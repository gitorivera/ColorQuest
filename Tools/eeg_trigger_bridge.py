#!/usr/bin/env python3
"""
ColorQuest EEG Trigger Bridge
==============================
Receives UDP marker packets from the Meta Quest running ColorQuest and
forwards the marker code to a BioSemi USB trigger box (or any serial-port
trigger interface) for real-time EEG event synchronisation.

How it works
------------
The Unity game (UdpEventSender.cs) sends an 11-byte UDP packet on every
cognitive event.  This script receives those packets, extracts the 1-byte
marker code, sends it to the BioSemi trigger box over serial (USB-CDC),
holds the line for <pulse_ms> milliseconds, then resets it to 0x00.
The BioSemi ActiveTwo records the pulse value in the Status channel so
the EEG analyser can epoch by event type.

Packet layout (11 bytes, little-endian) — defined in UdpEventSender.cs:
  [0]    marker_code     uint8   → forwarded verbatim to BioSemi
  [1]    level           uint8
  [2]    grid_size       uint8
  [3]    num_distractors uint8
  [4-5]  board_id        uint16
  [6-9]  rt_ms           int32   (reaction time, -1 = not applicable)
  [10]   extra           uint8   (event-specific payload)

Quick start
-----------
  pip install pyserial
  python eeg_trigger_bridge.py --dry-run          # test without hardware
  python eeg_trigger_bridge.py --list-ports       # find trigger box COM port
  python eeg_trigger_bridge.py --serial COM4      # run with BioSemi box on COM4

Python decode snippet for offline analysis:
  import struct
  code, lvl, gs, nd, bid, rt, extra = struct.unpack('<BBBBHiB', raw_bytes)
"""

from __future__ import annotations

import argparse
import csv
import logging
import signal
import socket
import struct
import sys
import threading
import time
from datetime import datetime, timezone
from pathlib import Path
from typing import Optional

try:
    import serial
    import serial.tools.list_ports
    SERIAL_AVAILABLE = True
except ImportError:
    SERIAL_AVAILABLE = False

# ──────────────────────────────────────────────────────────────────────────────
#  Packet definition
# ──────────────────────────────────────────────────────────────────────────────

PACKET_FORMAT = '<BBBBHiB'                        # little-endian
PACKET_SIZE   = struct.calcsize(PACKET_FORMAT)    # 11 bytes

# ──────────────────────────────────────────────────────────────────────────────
#  Marker codes — must stay in sync with UdpEventSender.cs
# ──────────────────────────────────────────────────────────────────────────────

MARKER_LABELS: dict[int, str] = {
    # Synchronisation / calibration
    0x01: 'SYNC_PULSE',
    # Session lifecycle
    0x10: 'SESSION_START',
    0x11: 'SESSION_END',
    # Difficulty / context
    0x20: 'LEVEL_START',
    0x21: 'LEVEL_END',
    0x22: 'DIFFICULTY_CHANGE',
    # Trial lifecycle
    0x30: 'BOARD_SPAWN',
    0x31: 'BOARD_TIMEOUT',
    0x32: 'BOARD_FAILED',
    # Working memory
    0x40: 'SEQUENCE_SHOWN',
    0x41: 'SEQUENCE_COMPLETED',
    # Response accuracy (core ADHD metrics)
    0x50: 'SPHERE_CORRECT',
    0x51: 'SPHERE_INCORRECT',
    0x52: 'DISTRACTOR_HIT',
    0x53: 'TARGET_MISSED',
    # Raw motor event
    0x60: 'TRIGGER_PULL',
    # Error monitoring
    0x70: 'ERROR_FEEDBACK_ONSET',
    0x71: 'ERROR_FEEDBACK_OFFSET',
    # Reward / motivation
    0x80: 'SCORE_INCREASE',
    0x81: 'SCORE_PENALTY',
    0x82: 'HAPTIC_FEEDBACK',
    # Vigilance / intra-individual variability
    0x90: 'SLOW_RESPONSE',
    0x91: 'FAST_RESPONSE',
}

# Markers most relevant for EEG epoch analysis in ADHD research.
# Used by the --eeg-only flag to suppress noise events.
EEG_CORE_MARKERS: frozenset[int] = frozenset({
    0x01,                               # SYNC_PULSE        — timing calibration
    0x10, 0x11,                         # SESSION lifecycle
    0x20,                               # LEVEL_START       — block onset
    0x30, 0x31, 0x32,                   # BOARD lifecycle   — trial onset/outcome
    0x40, 0x41,                         # SEQUENCE WM load/retrieval
    0x50, 0x51, 0x52, 0x53,            # Hit accuracy      — core ADHD metrics
    0x70, 0x71,                         # ERROR FEEDBACK    — error monitoring / N2
    0x80, 0x81,                         # REWARD feedback   — FRN / reward positivity
    0x90, 0x91,                         # VIGILANCE         — RT variability markers
})


# ──────────────────────────────────────────────────────────────────────────────
#  BioSemi trigger box interface
# ──────────────────────────────────────────────────────────────────────────────

class BioSemiTrigger:
    """
    Serial interface for the BioSemi USB trigger box (or any USB-CDC trigger
    device that accepts single-byte writes).

    The BioSemi ActiveTwo reads trigger values from a USB device that appears
    as a virtual COM port.  Writing a byte N sets the Status channel to N for
    <pulse_ms> ms, after which a 0x00 reset clears the line.

    If your lab uses a different trigger interface (parallel port, LabJack,
    Cedrus StimTracker …) swap out this class — the rest of the bridge is
    hardware-agnostic.
    """

    # USB-serial descriptors that commonly identify trigger boxes.
    # Add your device's description if auto-detect misses it.
    _KNOWN_KEYWORDS = ('biosemi', 'ftdi', 'ft232', 'usb serial', 'usb-serial',
                       'cp210', 'ch340', 'trigger', 'stimtracker')

    def __init__(self, port: Optional[str], baud: int = 115200,
                 pulse_ms: int = 10):
        self._port      = port
        self._baud      = baud
        self._pulse_s   = pulse_ms / 1000.0
        self._ser: Optional[serial.Serial] = None
        self._lock      = threading.Lock()
        self._timer: Optional[threading.Timer] = None

    # ── public ────────────────────────────────────────────────────────────────

    def connect(self) -> bool:
        if not SERIAL_AVAILABLE:
            logging.error("pyserial is not installed.  Run:  pip install pyserial")
            return False

        port = self._port or self._auto_detect()
        if port is None:
            logging.error("Trigger box not found.  Use --serial <port> or --dry-run.")
            return False

        try:
            self._ser = serial.Serial(port, self._baud, timeout=0.05)
            self._reset()
            logging.info(f"[TriggerBox] Connected on {port} @ {self._baud} baud  "
                         f"pulse={int(self._pulse_s * 1000)} ms")
            return True
        except serial.SerialException as exc:
            logging.error(f"[TriggerBox] Cannot open {port}: {exc}")
            return False

    def send(self, code: int) -> float:
        """
        Pulse the trigger line.  Returns wall-clock time (seconds since epoch)
        at the moment of the serial write — use this as the EEG marker timestamp.
        """
        with self._lock:
            if self._timer is not None:
                self._timer.cancel()
            epoch = time.time()
            if self._ser and self._ser.is_open:
                self._ser.write(bytes([code & 0xFF]))
                self._ser.flush()
            t = threading.Timer(self._pulse_s, self._reset)
            t.daemon = True
            t.start()
            self._timer = t
        return epoch

    def close(self):
        if self._timer:
            self._timer.cancel()
        with self._lock:
            if self._ser and self._ser.is_open:
                self._reset()
                time.sleep(self._pulse_s)
                self._ser.close()
        logging.info("[TriggerBox] Closed.")

    # ── private ───────────────────────────────────────────────────────────────

    def _reset(self):
        try:
            if self._ser and self._ser.is_open:
                self._ser.write(b'\x00')
                self._ser.flush()
        except Exception:
            pass

    def _auto_detect(self) -> Optional[str]:
        ports = list(serial.tools.list_ports.comports())
        for p in ports:
            desc = ((p.description or '') + ' ' + (p.manufacturer or '')).lower()
            if any(kw in desc for kw in self._KNOWN_KEYWORDS):
                logging.info(f"[TriggerBox] Auto-detected: {p.device}  ({p.description})")
                return p.device
        logging.warning("[TriggerBox] Auto-detect failed.  Available ports:")
        for p in ports:
            logging.warning(f"  {p.device:<12}  {p.description}")
        return None


# ──────────────────────────────────────────────────────────────────────────────
#  Dry-run stub (no hardware required)
# ──────────────────────────────────────────────────────────────────────────────

class DryRunTrigger:
    """Logs trigger pulses to console.  Use for testing without hardware."""

    def connect(self) -> bool:
        logging.info("[TriggerBox] DRY-RUN — no serial port opened.")
        return True

    def send(self, code: int) -> float:
        label = MARKER_LABELS.get(code, f'UNKNOWN_0x{code:02X}')
        logging.info(f"[TriggerBox] DRY-RUN  pulse 0x{code:02X}  ({label})")
        return time.time()

    def close(self):
        pass


# ──────────────────────────────────────────────────────────────────────────────
#  CSV event log
# ──────────────────────────────────────────────────────────────────────────────

_LOG_COLUMNS = [
    'pc_timestamp_iso', 'pc_unix_ms',
    'marker_code', 'marker_label',
    'level', 'grid_size', 'num_distractors',
    'board_id', 'rt_ms', 'extra',
]


class EventLog:
    """
    Writes every received packet to a CSV file together with the PC wall-clock
    timestamp at which the serial trigger was dispatched.

    Cross-check this file against the Quest's NDJSON EventLogger output to
    verify synchronisation quality and detect dropped UDP packets.
    """

    def __init__(self, path: Path):
        self._path  = path
        self._fh    = open(path, 'w', newline='', encoding='utf-8')
        self._csv   = csv.DictWriter(self._fh, fieldnames=_LOG_COLUMNS)
        self._csv.writeheader()
        self._fh.flush()
        logging.info(f"[EventLog] {path}")

    def write(self, trigger_epoch: float, packet: tuple) -> None:
        code, level, gs, nd, bid, rt, extra = packet
        dt = datetime.fromtimestamp(trigger_epoch, tz=timezone.utc)
        self._csv.writerow({
            'pc_timestamp_iso': dt.isoformat(),
            'pc_unix_ms':       int(trigger_epoch * 1000),
            'marker_code':      f'0x{code:02X}',
            'marker_label':     MARKER_LABELS.get(code, f'UNKNOWN_0x{code:02X}'),
            'level':            level,
            'grid_size':        gs,
            'num_distractors':  nd,
            'board_id':         bid,
            'rt_ms':            rt,
            'extra':            extra,
        })
        self._fh.flush()

    def close(self):
        self._fh.close()


# ──────────────────────────────────────────────────────────────────────────────
#  UDP receiver loop
# ──────────────────────────────────────────────────────────────────────────────

class UdpReceiver:

    def __init__(self, udp_port: int, trigger, log: Optional[EventLog],
                 eeg_only: bool = False):
        self._port     = udp_port
        self._trigger  = trigger
        self._log      = log
        self._eeg_only = eeg_only
        self._running  = False
        self._sock: Optional[socket.socket] = None
        self._stats    = {'received': 0, 'forwarded': 0, 'skipped': 0, 'errors': 0}

    def start(self):
        self._sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self._sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        self._sock.bind(('', self._port))
        self._sock.settimeout(1.0)   # lets the shutdown check run every second
        self._running = True
        logging.info(f"[UDP] Listening on 0.0.0.0:{self._port}  "
                     f"(eeg_only={'yes' if self._eeg_only else 'no'})")
        try:
            while self._running:
                try:
                    data, addr = self._sock.recvfrom(64)
                except socket.timeout:
                    continue
                self._handle(data, addr)
        finally:
            self._sock.close()

    def stop(self):
        self._running = False

    def print_stats(self):
        s = self._stats
        logging.info(
            f"[UDP] received={s['received']}  forwarded={s['forwarded']}  "
            f"skipped={s['skipped']}  errors={s['errors']}")

    # ── private ───────────────────────────────────────────────────────────────

    def _handle(self, data: bytes, addr: tuple) -> None:
        self._stats['received'] += 1

        if len(data) != PACKET_SIZE:
            logging.warning(f"[UDP] Unexpected packet size {len(data)} from {addr[0]}")
            self._stats['errors'] += 1
            return

        try:
            packet = struct.unpack(PACKET_FORMAT, data)
        except struct.error as exc:
            logging.warning(f"[UDP] Unpack error: {exc}")
            self._stats['errors'] += 1
            return

        code = packet[0]

        if self._eeg_only and code not in EEG_CORE_MARKERS:
            self._stats['skipped'] += 1
            return

        label = MARKER_LABELS.get(code, f'UNKNOWN_0x{code:02X}')
        logging.info(
            f"[EVENT]  0x{code:02X}  {label:<25s}  "
            f"lvl={packet[1]}  grid={packet[2]}  dist={packet[3]}  "
            f"board={packet[4]}  rt={packet[5]}ms  extra={packet[6]}")

        t_trigger = self._trigger.send(code)
        self._stats['forwarded'] += 1

        if self._log:
            self._log.write(t_trigger, packet)


# ──────────────────────────────────────────────────────────────────────────────
#  CLI helpers
# ──────────────────────────────────────────────────────────────────────────────

def cmd_list_ports():
    if not SERIAL_AVAILABLE:
        print("pyserial is not installed.  Run:  pip install pyserial")
        return
    ports = list(serial.tools.list_ports.comports())
    if not ports:
        print("No serial ports detected.")
        return
    print(f"\n{'Port':<12}  {'Description':<38}  Manufacturer")
    print('─' * 72)
    for p in ports:
        print(f"{p.device:<12}  {(p.description or ''):<38}  {p.manufacturer or ''}")
    print()


def _log_path(log_dir: str) -> Path:
    d = Path(log_dir)
    d.mkdir(parents=True, exist_ok=True)
    stamp = datetime.now().strftime('%Y%m%d_%H%M%S')
    return d / f'eeg_triggers_{stamp}.csv'


# ──────────────────────────────────────────────────────────────────────────────
#  Entry point
# ──────────────────────────────────────────────────────────────────────────────

def main():
    parser = argparse.ArgumentParser(
        description='ColorQuest → BioSemi EEG Trigger Bridge',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__)

    parser.add_argument(
        '--serial', metavar='PORT',
        help='COM port of the BioSemi USB trigger box (e.g. COM4, /dev/ttyUSB0). '
             'Omit to auto-detect.')
    parser.add_argument(
        '--baud', type=int, default=115200,
        help='Serial baud rate (default: 115200).')
    parser.add_argument(
        '--udp-port', type=int, default=9999, metavar='PORT',
        help='UDP port to bind on this PC (default: 9999).')
    parser.add_argument(
        '--pulse-ms', type=int, default=10, metavar='MS',
        help='Trigger pulse width in milliseconds (default: 10). '
             'BioSemi ActiveTwo samples at 2048 Hz (≈0.5 ms), so 10 ms is safe.')
    parser.add_argument(
        '--log-dir', default='logs',
        help='Directory for the CSV trigger log (default: ./logs).')
    parser.add_argument(
        '--no-log', action='store_true',
        help='Disable CSV event logging.')
    parser.add_argument(
        '--dry-run', action='store_true',
        help='Run without opening a serial port (console output only).')
    parser.add_argument(
        '--eeg-only', action='store_true',
        help='Forward only EEG-relevant markers; drop TRIGGER_PULL, HAPTIC, etc.')
    parser.add_argument(
        '--list-ports', action='store_true',
        help='Print available serial ports and exit.')
    parser.add_argument(
        '--verbose', action='store_true',
        help='Enable DEBUG-level logging.')

    args = parser.parse_args()

    if args.list_ports:
        cmd_list_ports()
        return

    logging.basicConfig(
        level=logging.DEBUG if args.verbose else logging.INFO,
        format='%(asctime)s.%(msecs)03d  %(message)s',
        datefmt='%H:%M:%S')

    if not SERIAL_AVAILABLE and not args.dry_run:
        logging.warning("pyserial not found — switching to --dry-run automatically.")
        args.dry_run = True

    # Build trigger interface
    trigger: BioSemiTrigger | DryRunTrigger
    if args.dry_run:
        trigger = DryRunTrigger()
    else:
        trigger = BioSemiTrigger(
            port=args.serial, baud=args.baud, pulse_ms=args.pulse_ms)

    if not trigger.connect():
        sys.exit(1)

    # Build event log
    log: Optional[EventLog] = None
    if not args.no_log:
        log = EventLog(_log_path(args.log_dir))

    # Build UDP receiver
    receiver = UdpReceiver(
        udp_port=args.udp_port,
        trigger=trigger,
        log=log,
        eeg_only=args.eeg_only)

    # Graceful shutdown on Ctrl-C / SIGTERM
    def _shutdown(sig=None, frame=None):
        logging.info("[Bridge] Shutting down …")
        receiver.stop()

    signal.signal(signal.SIGINT,  _shutdown)
    signal.signal(signal.SIGTERM, _shutdown)

    try:
        receiver.start()
    finally:
        receiver.print_stats()
        trigger.close()
        if log:
            log.close()
        logging.info("[Bridge] Done.")


if __name__ == '__main__':
    main()

#!/usr/bin/env python3
"""
ColorQuest LSL Markers Listener / Recorder
==========================================
Finds the 'ColorQuestMarkers' LSL stream broadcast by the Meta Quest, prints
each marker live, and saves the whole session to an .xdf file (the same format
LabRecorder writes, so it opens in MATLAB/MNE/SigViewer/pyxdf).

Unlike lsl_receiver.py (which only prints), this script also persists every
marker — with its LSL timestamp and recorded clock offsets — to disk.

Requirements:
    pip install pylsl

Usage:
    python lsl_markers_listener.py
    python lsl_markers_listener.py --name ColorQuestMarkers --timeout 30
    python lsl_markers_listener.py --output recordings/session1.xdf

Stop recording with Ctrl+C; the .xdf file is finalized on exit.

If the script cannot find the stream, check:
  - Quest and PC are on the same WiFi network / subnet
  - Router allows multicast UDP (home routers usually do; campus routers may block it)
  - Windows Firewall is not blocking incoming connections on ports 16571-16604
"""

import argparse
import os
import struct
import sys
import time
from datetime import datetime

# Print live instead of block-buffering, so markers show as they arrive even
# when stdout is piped. Removes any need for the `python -u` interpreter flag.
try:
    sys.stdout.reconfigure(line_buffering=True)
except (AttributeError, ValueError):
    pass

try:
    from pylsl import resolve_byprop, resolve_streams, StreamInlet, local_clock
except ImportError:
    print("[ERROR] pylsl not installed. Run:  pip install pylsl")
    sys.exit(1)


# --------------------------------------------------------------------------- #
# Minimal XDF writer
# --------------------------------------------------------------------------- #
# XDF is a chunked binary format. Each chunk is:
#   [NumLengthBytes:1][Length:NumLengthBytes][Tag:2][Content:Length-2]
# Length counts the 2-byte tag plus the content. See https://github.com/sccn/xdf
XDF_MAGIC = b"XDF:"

# Tags used here
TAG_FILE_HEADER   = 1   # XML
TAG_STREAM_HEADER = 2   # StreamID(uint32) + XML
TAG_SAMPLES       = 3   # StreamID(uint32) + sample data
TAG_CLOCK_OFFSET  = 4   # StreamID(uint32) + collection_time(double) + offset(double)
TAG_BOUNDARY      = 5   # 16-byte magic
TAG_STREAM_FOOTER = 6   # StreamID(uint32) + XML

BOUNDARY_UUID = bytes([0x43, 0xA5, 0x46, 0xDC, 0xCB, 0xF5, 0x41, 0x0F,
                       0xB3, 0x0E, 0xD5, 0x46, 0x73, 0x83, 0xB5, 0x36])


def _varlen(n):
    """Variable-length integer: a 1-byte width marker (1/4/8) then the value."""
    if n < 2 ** 8:
        return struct.pack("<BB", 1, n)
    if n < 2 ** 32:
        return struct.pack("<B", 4) + struct.pack("<I", n)
    return struct.pack("<B", 8) + struct.pack("<Q", n)


def _chunk(tag, content):
    """Wrap content in a length-prefixed, tagged XDF chunk."""
    length = 2 + len(content)          # 2 = the tag itself
    if length < 2 ** 8:
        header = struct.pack("<BB", 1, length)
    elif length < 2 ** 32:
        header = struct.pack("<B", 4) + struct.pack("<I", length)
    else:
        header = struct.pack("<B", 8) + struct.pack("<Q", length)
    return header + struct.pack("<H", tag) + content


def write_xdf(path, stream_id, stream_xml, samples, clock_offsets):
    """Write a single (string) marker stream to an .xdf file.

    samples       : list of (timestamp_float, [value_str, ...])
    clock_offsets : list of (collection_time_float, offset_float)
    """
    with open(path, "wb") as f:
        f.write(XDF_MAGIC)

        # FileHeader
        file_xml = b'<?xml version="1.0"?><info><version>1.0</version></info>'
        f.write(_chunk(TAG_FILE_HEADER, file_xml))

        # StreamHeader
        f.write(_chunk(TAG_STREAM_HEADER,
                       struct.pack("<I", stream_id) + stream_xml.encode("utf-8")))

        # Boundary marker (helps tools seek; harmless if ignored)
        f.write(_chunk(TAG_BOUNDARY, BOUNDARY_UUID))

        # ClockOffset chunks — let pyxdf synchronize to the recorder's clock
        for collection_time, offset in clock_offsets:
            f.write(_chunk(TAG_CLOCK_OFFSET,
                           struct.pack("<Idd", stream_id, collection_time, offset)))

        # Samples (markers are string-format, one channel)
        if samples:
            content = struct.pack("<I", stream_id) + _varlen(len(samples))
            for ts, values in samples:
                content += struct.pack("<B", 8)        # timestamp present (8 bytes)
                content += struct.pack("<d", ts)
                for v in values:
                    b = str(v).encode("utf-8")
                    content += _varlen(len(b)) + b
            f.write(_chunk(TAG_SAMPLES, content))

        # StreamFooter
        first_ts = samples[0][0] if samples else 0.0
        last_ts  = samples[-1][0] if samples else 0.0
        footer_xml = (
            '<?xml version="1.0"?><info>'
            f'<first_timestamp>{first_ts:.6f}</first_timestamp>'
            f'<last_timestamp>{last_ts:.6f}</last_timestamp>'
            f'<sample_count>{len(samples)}</sample_count>'
            '<clock_offsets></clock_offsets>'
            '</info>'
        )
        f.write(_chunk(TAG_STREAM_FOOTER,
                       struct.pack("<I", stream_id) + footer_xml.encode("utf-8")))


# --------------------------------------------------------------------------- #
# Recording
# --------------------------------------------------------------------------- #
def parse_args():
    p = argparse.ArgumentParser(description="ColorQuest LSL Markers Listener / Recorder")
    p.add_argument("--name",    default="ColorQuestMarkers", help="LSL stream name to record")
    p.add_argument("--timeout", type=float, default=30.0,    help="Discovery timeout in seconds")
    p.add_argument("--output",  default=None,                help="Output .xdf path (default: recordings/<name>_<timestamp>.xdf)")
    return p.parse_args()


def default_output(name):
    rec_dir = os.path.join(os.path.dirname(os.path.abspath(__file__)), "recordings")
    os.makedirs(rec_dir, exist_ok=True)
    stamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    return os.path.join(rec_dir, f"{name}_{stamp}.xdf")


def main():
    args = parse_args()
    out_path = args.output or default_output(args.name)

    print(f"[LSL] Searching for stream '{args.name}' on the local network ...")
    print(f"      (timeout = {args.timeout} s)\n")

    streams = resolve_byprop("name", args.name, minimum=1, timeout=args.timeout)
    if not streams:
        print(f"[ERROR] Stream '{args.name}' not found within {args.timeout} s.")
        others = resolve_streams(wait_time=2.0)
        if others:
            print("        Streams currently visible on the network:")
            for s in others:
                print(f"          - name='{s.name()}'  type='{s.type()}'  host='{s.hostname()}'")
            print(f"        → re-run with one of those, e.g.:  python lsl_markers_listener.py --name {others[0].name()}")
        else:
            print("        No LSL streams visible at all. Check that the Quest is running")
            print("        and on the same WiFi subnet (campus WiFi often blocks multicast).")
        sys.exit(1)

    inlet = StreamInlet(streams[0])
    info  = inlet.info()
    n_chan = info.channel_count()
    stream_id = abs(hash(info.uid())) % (2 ** 31) or 1   # any stable nonzero uint32
    stream_xml = info.as_xml()

    print(f"[OK]   Connected to: {info.name()} / {info.type()}")
    print(f"       Source ID   : {info.source_id()}")
    print(f"       Hostname    : {info.hostname()}")
    print(f"       Channels    : {n_chan}")
    print(f"       Saving to   : {out_path}")
    print()
    print(f"{'LSL TIME':>14}  {'CODE':>5}  {'LABEL':<26}  EXTRA")
    print("-" * 74)

    samples = []           # (timestamp, [values])
    clock_offsets = []     # (collection_time, offset)
    next_offset_t = 0.0    # forces an immediate first reading

    def record_offset():
        clock_offsets.append((local_clock(), inlet.time_correction()))

    try:
        while True:
            now = local_clock()
            if now >= next_offset_t:
                record_offset()
                next_offset_t = now + 5.0     # one clock-offset measurement / 5 s

            sample, timestamp = inlet.pull_sample(timeout=1.0)
            if sample is None:
                continue                       # timed out; loop to refresh offset / catch Ctrl+C

            samples.append((timestamp, list(sample)))

            raw   = sample[0]
            parts = raw.split("|", 2)
            code  = parts[0] if len(parts) > 0 else "?"
            label = parts[1] if len(parts) > 1 else "?"
            extra = parts[2] if len(parts) > 2 else ""
            print(f"{timestamp:>14.3f}  {code:>5}  {label:<26}  {extra}")
    except KeyboardInterrupt:
        print("\n[Recorder] Stopping ...")
    finally:
        record_offset()                        # final offset on the way out
        write_xdf(out_path, stream_id, stream_xml, samples, clock_offsets)
        print(f"[Recorder] Wrote {len(samples)} marker(s) to {out_path}")


if __name__ == "__main__":
    main()

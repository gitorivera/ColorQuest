#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
ColorQuest UDP trigger receiver -- macOS version
Recibe paquetes UDP del Quest y reenvía el código al trigger box BioSemi por serial.

Uso:
  python trigger_receiver_mac.py                        # auto-detecta puerto
  python trigger_receiver_mac.py --port /dev/cu.usbserial-A5069RR4
  python trigger_receiver_mac.py --list-ports           # ver puertos disponibles
  python trigger_receiver_mac.py --no-serial            # solo consola, sin hardware
"""
import argparse
import socket
import sys

try:
    import serial
    import serial.tools.list_ports
except ImportError:
    print("pyserial no instalado. Ejecuta:  pip3 install pyserial")
    sys.exit(1)

UDP_PORT = 9999

EVENT_LABELS = {
    0x01: "SPHERE_CORRECT",
    0x02: "SPHERE_INCORRECT",
    0x03: "SEQ_COMPLETED",
    0x04: "LEVEL_START",
    0x05: "GAME_END",
    0x06: "BOARD_SPAWN",
}

# USB-serial keywords that identify trigger boxes on macOS
_USB_KEYWORDS = ("usbserial", "usbmodem", "slab_usbtouart", "ftdi",
                 "cp210", "ch340", "ch9102", "biosemi", "trigger")


def list_ports():
    ports = list(serial.tools.list_ports.comports())
    if not ports:
        print("No se encontraron puertos seriales.")
        return
    print(f"\n{'Puerto':<35}  Descripcion")
    print("-" * 65)
    for p in ports:
        print(f"{p.device:<35}  {p.description or ''}")
    print()


def auto_detect():
    """
    En macOS los adaptadores USB-serie aparecen como /dev/cu.usbserial-*
    Usar cu.* (no tty.*) para conexiones salientes.
    """
    ports = list(serial.tools.list_ports.comports())
    # Priority 1: known USB-serial keywords
    for p in ports:
        combined = (p.device + " " + (p.description or "")).lower()
        if any(kw in combined for kw in _USB_KEYWORDS):
            print(f"Puerto auto-detectado: {p.device}  ({p.description})")
            return p.device
    # Priority 2: any cu.* device
    for p in ports:
        if "cu." in p.device:
            print(f"Puerto auto-detectado (fallback): {p.device}")
            return p.device
    return None


def main():
    parser = argparse.ArgumentParser(
        description="ColorQuest UDP trigger receiver (macOS)",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__)
    parser.add_argument("--port", "-p", default=None,
                        help="Puerto serial del trigger box "
                             "(ej: /dev/cu.usbserial-A5069RR4). "
                             "Si se omite, se intenta auto-detectar.")
    parser.add_argument("--udp-port", type=int, default=UDP_PORT,
                        help=f"Puerto UDP a escuchar (default: {UDP_PORT})")
    parser.add_argument("--list-ports", "-l", action="store_true",
                        help="Listar puertos seriales disponibles y salir")
    parser.add_argument("--no-serial", action="store_true",
                        help="Solo consola, sin enviar al trigger box (modo prueba)")
    args = parser.parse_args()

    if args.list_ports:
        list_ports()
        return

    ser = None
    if not args.no_serial:
        port = args.port or auto_detect()
        if port is None:
            print("ERROR: no se encontro puerto serial.")
            print("  Opciones:")
            print("    python trigger_receiver_mac.py --list-ports")
            print("    python trigger_receiver_mac.py --port /dev/cu.usbserial-XXXX")
            print("    python trigger_receiver_mac.py --no-serial")
            sys.exit(1)
        try:
            ser = serial.Serial(port, baudrate=115200,
                                bytesize=8, stopbits=1, parity="N")
            print(f"Conectado a {port} @ 115200 baud")
        except serial.SerialException as e:
            print(f"ERROR abriendo {port}: {e}")
            sys.exit(1)

    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.bind(("0.0.0.0", args.udp_port))
    sock.settimeout(1.0)

    mode = (ser.port if ser else "solo consola")
    print(f"Esperando triggers en UDP:{args.udp_port}  serial:{mode}  (Ctrl+C para salir)")

    try:
        while True:
            try:
                data, addr = sock.recvfrom(8)
            except socket.timeout:
                continue
            if data:
                code  = data[0]
                label = EVENT_LABELS.get(code, f"UNKNOWN_0x{code:02X}")
                if ser:
                    ser.write(bytes([code]))
                # ASCII-only arrow to avoid terminal encoding issues on macOS
                print(f"Evento!! -> 0x{code:02X}  {label:<20s}  desde {addr[0]}")
    except KeyboardInterrupt:
        print("\nSaliendo...")
    finally:
        sock.close()
        if ser:
            ser.close()


if __name__ == "__main__":
    main()

import argparse
import socket
import sys
import serial
import serial.tools.list_ports

UDP_PORT = 9999

EVENT_LABELS = {
    0x01: "SPHERE_CORRECT",
    0x02: "SPHERE_INCORRECT",
    0x03: "SEQ_COMPLETED",
    0x04: "LEVEL_START",
    0x05: "GAME_END",
    0x06: "BOARD_SPAWN",
}


def list_ports():
    ports = list(serial.tools.list_ports.comports())
    if not ports:
        print("No se encontraron puertos seriales.")
        return
    print(f"\n{'Puerto':<12}  Descripcion")
    print("-" * 50)
    for p in ports:
        print(f"{p.device:<12}  {p.description}")
    print()


def main():
    parser = argparse.ArgumentParser(
        description="ColorQuest UDP trigger receiver (Windows)")
    parser.add_argument("--port", "-p", default="COM3",
                        help="Puerto COM del trigger box (default: COM3)")
    parser.add_argument("--udp-port", type=int, default=UDP_PORT,
                        help=f"Puerto UDP a escuchar (default: {UDP_PORT})")
    parser.add_argument("--list-ports", "-l", action="store_true",
                        help="Listar puertos COM disponibles y salir")
    parser.add_argument("--no-serial", action="store_true",
                        help="Solo consola, sin enviar al trigger box (modo prueba)")
    args = parser.parse_args()

    if args.list_ports:
        list_ports()
        return

    ser = None
    if not args.no_serial:
        try:
            ser = serial.Serial(args.port, baudrate=115200,
                                bytesize=8, stopbits=1, parity="N")
            print(f"Conectado a {args.port} @ 115200 baud")
        except serial.SerialException as e:
            print(f"ERROR abriendo {args.port}: {e}")
            print("Usa --list-ports para ver puertos disponibles.")
            sys.exit(1)

    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.bind(("0.0.0.0", args.udp_port))
    sock.settimeout(1.0)

    mode = args.port if not args.no_serial else "solo consola"
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
                print(f"Evento!! → 0x{code:02X}  {label:<20s}  desde {addr[0]}")
    except KeyboardInterrupt:
        print("\nSaliendo...")
    finally:
        sock.close()
        if ser:
            ser.close()


if __name__ == "__main__":
    main()

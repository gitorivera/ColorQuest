import socket
import serial

COM_PORT = "COM3"   # ajusta al COM que asignó Windows al cable BioSemi
UDP_PORT = 9999

EVENT_LABELS = {
    0x01: "SPHERE_CORRECT",
    0x02: "SPHERE_INCORRECT",
    0x03: "SEQ_COMPLETED",
    0x04: "LEVEL_START",
    0x05: "GAME_END",
    0x06: "BOARD_SPAWN",
}

ser = serial.Serial(COM_PORT, baudrate=115200, bytesize=8, stopbits=1, parity='N')
sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
sock.bind(("0.0.0.0", UDP_PORT))
sock.settimeout(1.0)  # wake up every second so Ctrl+C is handled promptly

print("Esperando triggers... (Ctrl+C para salir)")

try:
    while True:
        try:
            data, addr = sock.recvfrom(8)
        except socket.timeout:
            continue
        if data:
            code  = data[0]
            label = EVENT_LABELS.get(code, f"UNKNOWN_0x{code:02X}")
           # ser.write(bytes([code]))
            print(f"Evento!! → 0x{code:02X}  {label:<20s}  desde {addr[0]}")
except KeyboardInterrupt:
    print("\nSaliendo...")
finally:
    sock.close()
    ser.close()
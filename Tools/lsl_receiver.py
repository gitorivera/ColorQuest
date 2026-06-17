#!/usr/bin/env python3
"""
ColorQuest LSL Receiver
=======================
Connects to the 'ColorQuestMarkers' LSL stream broadcast by the Meta Quest
and prints each marker as it arrives. No bridge or relay needed — the Quest
runs liblsl natively and advertises the stream on the local network.

Requirements:
    pip install pylsl

Usage:
    python lsl_receiver.py
    python lsl_receiver.py --name ColorQuestMarkers --timeout 30

If the script cannot find the stream, check:
  - Quest and PC are on the same WiFi network / subnet
  - Router allows multicast UDP (most home routers do; campus routers may block it)
  - Windows Firewall is not blocking incoming connections on ports 16571-16604
"""

import argparse
import sys
import time

# Print live instead of block-buffering, so markers show as they arrive even
# when stdout is piped. Removes any need for the `python -u` interpreter flag.
try:
    sys.stdout.reconfigure(line_buffering=True)
except (AttributeError, ValueError):
    pass

try:
    from pylsl import resolve_byprop, resolve_streams, StreamInlet
except ImportError:
    print("[ERROR] pylsl not installed. Run:  pip install pylsl")
    sys.exit(1)


def parse_args():
    p = argparse.ArgumentParser(description="ColorQuest LSL Receiver")
    p.add_argument("--name",    default="ColorQuestMarkers", help="LSL stream name to connect to")
    p.add_argument("--timeout", type=float, default=30.0,    help="Discovery timeout in seconds")
    return p.parse_args()


def main():
    args = parse_args()

    print(f"[LSL] Searching for stream '{args.name}' on the local network ...")
    print(f"      (timeout = {args.timeout} s)\n")

    streams = resolve_byprop("name", args.name, minimum=1, timeout=args.timeout)
    if not streams:
        print(f"[ERROR] Stream '{args.name}' not found within {args.timeout} s.")
        # Show whatever LSL streams ARE visible, so a wrong --name is obvious.
        others = resolve_streams(wait_time=2.0)
        if others:
            print("        Streams currently visible on the network:")
            for s in others:
                print(f"          - name='{s.name()}'  type='{s.type()}'  host='{s.hostname()}'")
            print(f"        → re-run with one of those, e.g.:  python lsl_receiver.py --name {others[0].name()}")
        else:
            print("        No LSL streams visible at all. Check that the Quest is running")
            print("        and on the same WiFi subnet (campus WiFi often blocks multicast).")
        sys.exit(1)

    inlet = StreamInlet(streams[0])
    info  = inlet.info()
    print(f"[OK]   Connected to: {info.name()} / {info.type()}")
    print(f"       Source ID   : {info.source_id()}")
    print(f"       Hostname    : {info.hostname()}")
    print()
    print(f"{'LSL TIME':>12}  {'CODE':>5}  {'LABEL':<26}  EXTRA")
    print("-" * 72)

    try:
        while True:
            sample, timestamp = inlet.pull_sample()
            raw    = sample[0]
            parts  = raw.split("|", 2)
            code   = parts[0] if len(parts) > 0 else "?"
            label  = parts[1] if len(parts) > 1 else "?"
            extra  = parts[2] if len(parts) > 2 else ""
            print(f"{timestamp:>12.3f}  {code:>5}  {label:<26}  {extra}")
    except KeyboardInterrupt:
        print("\n[Receiver] Stopped.")


if __name__ == "__main__":
    main()

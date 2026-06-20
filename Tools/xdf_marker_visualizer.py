#!/usr/bin/env python3
"""
ColorQuest XDF Marker Visualizer
================================
Reads an .xdf file recorded by lsl_markers_listener.py and prints all the
relevant information about the marker stream(s) inside it: stream metadata,
every marker with its timestamp and time relative to the recording start, and
summary counts per marker code/label.

Requirements:
    pip install pyxdf

Usage:
    python xdf_marker_visualizer.py                       # newest .xdf in ./recordings or .
    python xdf_marker_visualizer.py session.xdf
    python xdf_marker_visualizer.py session.xdf --stream ColorQuestMarkers
"""

import argparse
import glob
import os
import sys
from collections import Counter

try:
    import pyxdf
except ImportError:
    print("[ERROR] pyxdf not installed. Run:  pip install pyxdf")
    sys.exit(1)


def find_default_xdf():
    """Pick the most recently modified .xdf next to this script (or in cwd)."""
    here = os.path.dirname(os.path.abspath(__file__))
    candidates = []
    for pattern in (os.path.join(here, "recordings", "*.xdf"),
                    os.path.join(here, "*.xdf"),
                    "*.xdf"):
        candidates.extend(glob.glob(pattern))
    if not candidates:
        return None
    return max(candidates, key=os.path.getmtime)


def parse_args():
    p = argparse.ArgumentParser(description="ColorQuest XDF Marker Visualizer")
    p.add_argument("xdf", nargs="?", default=None, help="Path to the .xdf file (default: newest found)")
    p.add_argument("--stream", default=None, help="Only show the stream with this name")
    return p.parse_args()


def meta(info, key, default=""):
    """Pull a single scalar value out of a pyxdf stream 'info' dict."""
    v = info.get(key)
    if isinstance(v, list) and v:
        return v[0]
    return v if v is not None else default


def is_marker_stream(info):
    """Irregular-rate or string-format streams are treated as markers."""
    srate = meta(info, "nominal_srate", "0")
    fmt   = meta(info, "channel_format", "")
    try:
        srate = float(srate)
    except (TypeError, ValueError):
        srate = 0.0
    return srate == 0.0 or fmt == "string"


def show_stream(stream):
    info       = stream["info"]
    name       = meta(info, "name")
    stype      = meta(info, "type")
    source_id  = meta(info, "source_id")
    hostname   = meta(info, "hostname")
    n_chan     = meta(info, "channel_count")
    fmt        = meta(info, "channel_format")
    srate      = meta(info, "nominal_srate")
    created_at = meta(info, "created_at")

    stamps = stream["time_stamps"]
    series = stream["time_series"]
    count  = len(stamps)

    print("=" * 78)
    print(f"STREAM: {name}")
    print("=" * 78)
    print(f"  Type           : {stype}")
    print(f"  Source ID      : {source_id}")
    print(f"  Hostname       : {hostname}")
    print(f"  Channels       : {n_chan}   Format: {fmt}   Nominal srate: {srate}")
    print(f"  Created at      : {created_at}")
    print(f"  Marker count   : {count}")

    if count == 0:
        print("  (no markers recorded)\n")
        return

    t0, t1 = stamps[0], stamps[-1]
    print(f"  First timestamp: {t0:.3f} s")
    print(f"  Last timestamp : {t1:.3f} s")
    print(f"  Duration       : {t1 - t0:.3f} s")
    print()

    # --- every marker -----------------------------------------------------
    print(f"  {'#':>4}  {'LSL TIME':>14}  {'t+ (s)':>9}  {'CODE':>5}  {'LABEL':<24}  EXTRA")
    print("  " + "-" * 90)

    code_counts  = Counter()
    label_counts = Counter()

    for i, (ts, row) in enumerate(zip(stamps, series), start=1):
        raw   = row[0] if len(row) else ""
        parts = str(raw).split("|", 2)
        code  = parts[0] if len(parts) > 0 else "?"
        label = parts[1] if len(parts) > 1 else ""
        extra = parts[2] if len(parts) > 2 else ""
        code_counts[code]   += 1
        label_counts[label] += 1
        print(f"  {i:>4}  {ts:>14.3f}  {ts - t0:>9.3f}  {code:>5}  {label:<24}  {extra}")

    # --- summary ----------------------------------------------------------
    print()
    print("  Summary by code:")
    for code, n in sorted(code_counts.items(), key=lambda kv: (-kv[1], kv[0])):
        print(f"    code {code:>5} : {n}")

    if any(label_counts):
        print("  Summary by label:")
        for label, n in sorted(label_counts.items(), key=lambda kv: (-kv[1], kv[0])):
            if label:
                print(f"    {label:<24} : {n}")
    print()


def main():
    args = parse_args()

    path = args.xdf or find_default_xdf()
    if not path:
        print("[ERROR] No .xdf file given and none found in ./recordings or current dir.")
        print("        Usage: python xdf_marker_visualizer.py <file.xdf>")
        sys.exit(1)
    if not os.path.isfile(path):
        print(f"[ERROR] File not found: {path}")
        sys.exit(1)

    print(f"[XDF] Loading {path}\n")
    streams, header = pyxdf.load_xdf(path)

    if header and header.get("info"):
        print(f"  File format version: {meta(header['info'], 'version')}")
    print(f"  Streams in file    : {len(streams)}\n")

    shown = 0
    for stream in streams:
        name = meta(stream["info"], "name")
        if args.stream and name != args.stream:
            continue
        if args.stream is None and not is_marker_stream(stream["info"]):
            # Skip continuous signal streams unless explicitly requested.
            print(f"(skipping non-marker stream '{name}'; pass --stream {name} to view it)\n")
            continue
        show_stream(stream)
        shown += 1

    if shown == 0:
        print("[INFO] No marker streams shown. Available streams:")
        for stream in streams:
            print(f"   - {meta(stream['info'], 'name')}  (type={meta(stream['info'], 'type')})")


if __name__ == "__main__":
    main()

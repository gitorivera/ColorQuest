# ColorQuest — Tools

Python utilities for receiving, recording, and inspecting the LSL marker stream
broadcast by the ColorQuest VR app running on the Meta Quest.

The Quest runs **liblsl natively** and advertises its marker stream on the local
network — no bridge or relay is needed. The PC scripts here just discover and
consume that stream.

**Stream details**

| Property   | Value                |
|------------|----------------------|
| Name       | `ColorQuestMarkers`  |
| Type       | `Markers`            |
| Source ID  | `colorquest-adhd-v1` |
| Channels   | 1 (string)           |
| Rate       | irregular            |

Each marker is a single string of the form `code|label|extra` (e.g.
`1|TargetSpawn|red`).

## Requirements

- `pylsl` — used by `lsl_receiver.py` and `lsl_markers_listener.py`.
- `pyxdf` — used by `xdf_marker_visualizer.py`.

### Option A — Miniconda (recommended)

Use the provided `requirements.yml` to create an isolated environment. The
conda-forge `pylsl` package bundles the native liblsl, so nothing else is needed.

```
conda env create -f requirements.yml
conda activate colorquest-lsl
```

Update or remove it later with:

```
conda env update -f requirements.yml --prune
conda env remove -n colorquest-lsl
```

### Option B — pip

```
pip install pylsl pyxdf
```

## Scripts

### `lsl_receiver.py` — live view only

Connects to the `ColorQuestMarkers` stream and prints each marker as it arrives.
Does **not** save anything; use it for a quick connectivity check.

```
python lsl_receiver.py
python lsl_receiver.py --name ColorQuestMarkers --timeout 30
```

| Option      | Default             | Description                       |
|-------------|---------------------|-----------------------------------|
| `--name`    | `ColorQuestMarkers` | LSL stream name to connect to     |
| `--timeout` | `30`                | Discovery timeout in seconds      |

If the stream isn't found, it lists whatever LSL streams *are* visible so a wrong
`--name` is obvious.

### `lsl_markers_listener.py` — live view **and** record to `.xdf`

Same live print as `lsl_receiver.py`, but also saves the whole session to an
`.xdf` file (the same chunked format LabRecorder writes, so it opens in
MATLAB / MNE / SigViewer / pyxdf). Stop with **Ctrl+C** — the file is finalized
on exit.

```
python lsl_markers_listener.py
python lsl_markers_listener.py --name ColorQuestMarkers --timeout 30
python lsl_markers_listener.py --output recordings/session1.xdf
```

| Option      | Default                                          | Description                  |
|-------------|--------------------------------------------------|------------------------------|
| `--name`    | `ColorQuestMarkers`                              | LSL stream name to record    |
| `--timeout` | `30`                                             | Discovery timeout in seconds |
| `--output`  | `recordings/<name>_<timestamp>.xdf`              | Output `.xdf` path           |

The written `.xdf` contains the full stream metadata (StreamHeader), every marker
with its LSL timestamp (Samples), and periodic clock-offset measurements
(ClockOffset chunks, one every 5 s plus one on exit) so tools like pyxdf can
clock-synchronize the recording. The XDF writer is self-contained — no
dependency beyond `pylsl`.

### `xdf_marker_visualizer.py` — inspect a recording

Reads an `.xdf` file (written by `lsl_markers_listener.py` or LabRecorder) and
prints all the relevant marker information.

```
python xdf_marker_visualizer.py                   # newest .xdf in ./recordings or .
python xdf_marker_visualizer.py session.xdf
python xdf_marker_visualizer.py session.xdf --stream ColorQuestMarkers
```

| Option     | Default        | Description                                   |
|------------|----------------|-----------------------------------------------|
| `xdf`      | newest found   | Path to the `.xdf` file                       |
| `--stream` | all markers    | Only show the stream with this name           |

Output includes:
- **Stream metadata** — name, type, source id, hostname, channel count/format,
  nominal rate, created-at, first/last timestamp, duration.
- **Every marker** — index, LSL timestamp, time relative to recording start, and
  the parsed `code | label | extra` fields.
- **Summary** — counts per marker code and per label.

Continuous signal streams (regular rate, non-string) are skipped by default;
pass `--stream <name>` to view one explicitly.

## Typical workflow

```
python lsl_markers_listener.py      # records markers until Ctrl+C
python xdf_marker_visualizer.py     # inspects the newest recording
```

## Troubleshooting

If a script can't find the stream:

- The Quest and PC must be on the **same WiFi network / subnet**. Campus WiFi
  often blocks multicast UDP, which LSL uses for discovery.
- Keep the headset **on / active** — the proximity sensor suspends the app when
  off-head, which closes the LSL outlet and causes a false "stream not found".
- Make sure Windows Firewall isn't blocking incoming connections on ports
  **16571–16604**.
- Home routers usually allow the multicast LSL needs; managed/enterprise routers
  may not.

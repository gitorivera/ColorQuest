# ColorQuest

[![DOI](https://zenodo.org/badge/DOI/10.5281/zenodo.23373466.svg)](https://doi.org/10.5281/zenodo.23373466)

**English** | [Español](README.es.md)

ColorQuest is a mixed-reality attention-training game for the Meta Quest, built with Unity as a research instrument for ADHD studies. It records behavioral events and streams time-stamped markers over [Lab Streaming Layer (LSL)](https://labstreaminglayer.org/) so game events can be aligned with EEG and other biosignals.

Developed at the Universidad Tecnológica de Pereira (UTP), Colombia.

## Gameplay

A grid of colored spheres appears on the board. The player shoots them with the controller in the 3-color order shown in a reference panel. A wrong order triggers a red grid flash, controller haptics and a short 2.5 s pause; a correct sequence awards points and confetti.

The game has five levels of increasing difficulty (about 20 s each, progressing automatically):

| Level | Grid | Sequence length |
|---|---|---|
| 1 | 2×2 | 3 |
| 2 | 3×3 | 4 |
| 3 | 4×4 | 5 |
| 4 | 5×5 | 6 |
| 5 | 6×6 | 7 |

## Research instrumentation

- **`EventLogger.cs`** – appends events as NDJSON to `Application.persistentDataPath` for offline analysis. Participant IDs are pseudonymized (no personal data).
- **`LSLMarkerSender.cs`** – runs liblsl natively on the Quest and publishes the `ColorQuestMarkers` stream (type `Markers`, source id `colorquest-adhd-v1`). Markers are strings of the form `code|label|extra`.
- **`SyncFlash.cs`** – shows a head-locked white flash with a simultaneous LSL marker (`sync_k`) so screen recordings can be aligned to LSL time. See [GAME-SPECS.md](GAME-SPECS.md).
- **`Tools/`** – Python utilities to receive, record and inspect the stream on a PC. See [Tools/README.md](Tools/README.md).

## Requirements

- Unity **6000.4.0f1** with Android Build Support
- Meta XR SDK 203.0.0 and OpenXR (see `Packages/manifest.json`)
- Meta Quest 3 (or compatible) with developer mode enabled
- For data collection: a PC on the **same subnet** as the headset, with Python and `pylsl`

## Getting started

1. Clone the repository and open the folder with Unity 6000.4.0f1.
2. Open `Assets/Scenes/DefaultScene.unity`.
3. Switch the build target to Android, connect the Quest and use *Build And Run*.
4. On the PC, receive the markers:

   ```bash
   pip install pylsl
   python Tools/lsl_receiver.py
   ```

Notes on LSL over Wi-Fi: many campus networks block multicast. If the stream is not discovered, add the headset's IP to an `lsl_api.cfg` (`[lab]` → `KnownPeers = {<quest-ip>}`) and point the `LSLAPICFG` environment variable at it. Keep the headset on your head while testing; the proximity sensor suspends the app otherwise.

## Repository layout

| Path | Content |
|---|---|
| `Assets/scripts/` | Game logic (`GameController`, `Blaster`, `EventLogger`) |
| `Assets/scripts/LSL/` | LSL marker sender and sync flash |
| `Assets/Plugins/Android/` | Android `liblsl.so` and manifest (network permissions) |
| `Tools/` | Python tools for the LSL stream |
| `documentation/` | Additional documentation |
| `GAME-SPECS.md` | Game ↔ TET/EEG synchronization specification (Spanish) |

## License

The project's own code and documentation are released under the [MIT License](LICENSE).

Third-party components keep their own licenses: the Meta XR SDK is subject to Meta's terms, and LSL4Unity / liblsl are MIT-licensed. Unity itself is subject to Unity's terms.

## How to cite

If you use ColorQuest in your research, please cite it. See [CITATION.cff](CITATION.cff) or use the DOI: [10.5281/zenodo.23373466](https://doi.org/10.5281/zenodo.23373466).

## Author

Jorge Rivera – Universidad Tecnológica de Pereira (j.rivera@utp.edu.co)

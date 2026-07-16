# ColorQuest — LSL Marker Synchronization: Error Sources & Recommendations

This document analyzes the timing/synchronization of the LSL event markers in
ColorQuest and lists concrete recommendations. It is the result of an end-to-end
review of the marker pipeline:

- `Assets/scripts/LSL/LSLMarkerSender.cs` — opens the LSL outlet, pushes markers
- `Assets/scripts/EventLogger.cs` — calls the sender alongside NDJSON logging
- `Assets/scripts/GameController.cs` — game logic that fires the events
- `Assets/scripts/Blaster.cs` — VR trigger input → sphere hits
- `Tools/lsl_receiver.py` — live console viewer
- `Tools/xdf_marker_visualizer.py` — offline XDF inspection (implies LabRecorder)

---

## How timestamps are actually assigned

The key line is `LSLMarkerSender.cs`:

```csharp
outlet.push_sample(new[] { marker });
```

`push_sample` is called **without an explicit timestamp**, so liblsl stamps the
sample with `local_clock()` *at the instant that C# line executes* — on the Unity
main thread, inside the frame where the game logic decided the event happened.
That single fact drives every sync error source below.

**Good news first:** WiFi latency does **not** corrupt the timestamps. The
timestamp is assigned on the Quest at push time and travels *with* the sample.
Network jitter changes when the receiver *sees* a marker, not its recorded time.
The relevant "lag" is therefore not transport lag — it is the gap between the
*real-world event* and the *line of code that stamps it*.

---

## Error sources, ranked by impact

### 1. Photon-to-marker offset on stimulus events — biggest systematic error
`board_spawn` and `sequence_shown` are the epoch anchors (the code comments say
"trial onset (epoch anchor)" and "P300"). They are stamped the moment
`Instantiate(...)` is called in `PosicionarBolas` / `PonerSecuencia`. The spheres
do not reach the participant's retina then — the marker is stamped *before* the
frame is even rendered, then the OVR compositor and display scanout add more. On
Quest this is roughly **20–40 ms of constant delay** between the marker and the
actual visual onset. This is a **bias**, not jitter — the worst kind for ERP
latency measurement (e.g. P300 latency).

### 2. Input-polling frame quantization — the "frame-to-frame lag"
`Blaster.Update()` polls `OVRInput.GetDown(...)` and `EsferaClickeada` runs in the
same frame. The physical trigger press happened somewhere in the *previous* frame
interval, so the marker is quantized to the frame boundary:

| Refresh rate | Max jitter | Mean | ~SD |
|--------------|-----------|------|-----|
| 72 Hz        | 13.9 ms   | ~7 ms | ~4 ms |
| 90 Hz        | 11.1 ms   | ~5.5 ms | ~3 ms |
| 120 Hz       | 8.3 ms    | ~4 ms | ~2.4 ms |

This is **uniform random jitter**, not bias.

### 3. Cross-device clock alignment (Quest clock ≠ EEG clock)
Marker timestamps live in the Quest's `local_clock` domain. To epoch EEG they must
be mapped into the amplifier's clock. LSL solves this via per-stream
`time_correction()` offsets — **but only if they are recorded.**

- `lsl_receiver.py` prints the raw sender-domain timestamp with **no
  `time_correction()`** and does not enable `proc_clocksync`. Fine for a live
  sanity check, useless for alignment.
- `xdf_marker_visualizer.py` reads XDF, which means recording happens via
  **LabRecorder** — good, that captures clock-offset samples and pyxdf
  dejitters/aligns them. **Ensure EEG and markers are recorded into the same XDF
  by the same LabRecorder instance.** Never align by hand using the printed
  receiver timestamps — that offset is uncorrected and drifts.

### 4. GC / instantiation hitches inflate jitter on specific frames
The 6×6 grid (`CrearMalla`), confetti, the ray `LineRenderer`, and
`Destroy`/`Instantiate` churn can spike a frame past its nominal interval. A
marker stamped on a hitched frame inherits the whole hitch as added delay. The
jitter distribution therefore has a tail on heavy frames (level transitions,
confetti).

### 5. Redundant / co-timestamped markers
In the error branch of `EsferaClickeada` (`GameController.cs`) three markers are
pushed in the same frame for one button press: `sphere_hit_incorrect`,
`board_failed`, and `error_pause_enter` — within microseconds of each other. Not a
sync error per se, but it complicates epoching and inflates code 41/43/50 counts.
Decide which one is the ERN anchor.

---

## Do you have to deal with the frame-to-frame lag?

It depends on the signal — and it is mostly **not** the dominant problem:

- **Behavioral RT analysis** (`rt_board`, `rt_prev`): **No.** Those RTs are computed
  from `Time.realtimeSinceStartup` captured *on the same frame* as the marker, so
  the frame quantization cancels — RT and marker are internally consistent.
- **Slow/broad ERP components** (P300, post-error slowing, vigilance): a ±7 ms
  jitter is negligible against a component hundreds of ms wide. Don't bother
  fixing input polling.
- **Sharp/early components or precise latency stats** (ERN, N2, onset latency to
  the millisecond): yes — but fix in priority order: **#1 (constant photon
  offset) before #2 (random frame jitter)**, because a 20–40 ms bias dwarfs a
  ~7 ms mean jitter.

---

## Recommendations (in priority order)

1. **Always record with LabRecorder into one XDF** (markers + EEG together) and let
   pyxdf apply clock sync. Do not trust `lsl_receiver.py` timestamps for analysis.
2. **Measure and subtract display latency once.** Put a photodiode on the HMD lens
   (or use an external high-speed camera) and flash a high-contrast quad on the
   frame you push `board_spawn`; the photodiode-vs-marker delta is your constant
   offset. Subtract it from all visual-onset markers offline. This removes error
   source #1 cleanly.
3. **Push stimulus markers from end-of-frame, not from game logic.** Move the
   `SendBoardSpawn` / `SendSequenceShown` push into a `WaitForEndOfFrame` coroutine
   (or `Camera.onPostRender`) so it is stamped after render submission. Closes part
   of the gap; the photodiode still handles the irreducible compositor/scanout
   part.
4. **Run the headset at 90 or 120 Hz** if not already (`OVRManager` display
   frequency) — halves input jitter for free and reduces hitch probability.
5. **Do not add explicit `push_sample(marker, timestamp)` for input events** unless
   a truer timestamp than "now" is available. OVR does not expose a sub-frame
   hardware press timestamp here, so passing `local_clock()` manually gains
   nothing over the default.
6. **Keep `nominal_srate = 0`** — correct for irregular markers; do not change it.

---

## Summary

The frame-to-frame input lag is real but small (~one frame) and self-consistent
with the logged RTs — for most ADHD measures it can be left as-is. The offset that
should be actively addressed is the **constant display/render delay on
stimulus-onset markers** (recommendations #2 and #3), together with the discipline
of **recording clock-corrected XDF** (recommendation #1).

using System;
using UnityEngine;
using LSL;   // requires LSL4Unity — install via Package Manager (see below)

/// <summary>
/// Broadcasts ADHD-relevant event markers over Lab Streaming Layer (LSL).
/// LSL4Unity wraps liblsl: stream discovery, transport and timestamp sync
/// are all handled by the library — no relay script needed on the PC.
///
/// Any LSL consumer on the same WiFi network (LabRecorder, BrainVision Recorder,
/// OpenBCI GUI, EEGlab, MNE-Python) will see the stream automatically as:
///     name   = "ColorQuestMarkers"
///     type   = "Markers"
///     format = string
///
/// ── INSTALL LSL4Unity ────────────────────────────────────────────────────────
///   Window → Package Manager → + → Add package from Git URL:
///   https://github.com/labstreaminglayer/LSL4Unity.git
///
///   If Android ARM64 (.so) is missing after install, copy liblsl.so
///   for arm64-v8a into Assets/Plugins/Android/arm64-v8a/
///   and set its platform to Android in the Inspector.
/// ─────────────────────────────────────────────────────────────────────────────
///
/// Marker format (one string per event pushed to the LSL outlet):
///     "CODE|LABEL[|key=val,key=val...]"
///     e.g. "40|sphere_hit_correct|id=bola1,pos=1,rt_board=1250,rt_prev=830"
///
/// Marker code table (ADHD-relevant events only):
///   10  session_start          — recording epoch boundary
///   11  session_end
///   21  level_start_1          — working-memory load level 1 (2×2, 3 balls)
///   22  level_start_2          — level 2 (3×3, 4 balls)
///   23  level_start_3          — level 3 (4×4, 5 balls)
///   24  level_start_4          — level 4 (5×5, 6 balls)
///   25  level_start_5          — level 5 (6×6, 7 balls)
///   30  board_spawn            — trial onset (epoch anchor)
///   31  sequence_shown         — stimulus presentation → P300
///   40  sphere_hit_correct     — correct response  → sustained attention / RT
///   41  sphere_hit_incorrect   — commission error  → impulsivity
///   42  board_timeout          — omission error    → inattention / vigilance
///   43  board_failed
///   50  error_pause_enter      — post-error inhibition onset → ERN
///   51  error_pause_exit       — inhibition recovery / post-error slowing
///
/// ── Stream TET (GAME-SPECS.md §4–§5) ─────────────────────────────────────────
/// A second outlet carries plain-label markers for the TET (temporal experience
/// tracing) app, so its CSV export only contains events it understands:
///     name = "JuegoEventos", type = "Markers", source_id = "juego-rm-quest"
///     labels: sync_1, sync_2, …, "inicio partida", "inicio nivel N",
///             "acierto …", "error …", "premio …", "fin partida"
/// Both outlets share the same LSL clock, so they align in the same XDF.
/// </summary>
public class LSLMarkerSender : MonoBehaviour
{
    public static LSLMarkerSender Instance { get; private set; }

    [Header("LSL Stream")]
    [Tooltip("Stream name visible to EEG acquisition software on the network")]
    [SerializeField] private string streamName = "ColorQuestMarkers";

    [Tooltip("Uncheck to disable all markers without removing calls from EventLogger")]
    [SerializeField] private bool markersEnabled = true;

    [Header("LSL Stream TET")]
    [Tooltip("Stream with plain-label markers for the TET app (sync_k, inicio partida, …)")]
    [SerializeField] private string tetStreamName = "JuegoEventos";
    [Tooltip("Fixed source_id so LabRecorder reconnects if the stream drops")]
    [SerializeField] private string tetSourceId = "juego-rm-quest";

    // ── Marker codes ──────────────────────────────────────────────────────────
    public static class Codes
    {
        public const int SESSION_START        = 10;
        public const int SESSION_END          = 11;
        public const int LEVEL_BASE           = 20;   // add level 1-5 → 21-25
        public const int BOARD_SPAWN          = 30;
        public const int SEQUENCE_SHOWN       = 31;
        public const int SPHERE_HIT_CORRECT   = 40;
        public const int SPHERE_HIT_INCORRECT = 41;
        public const int BOARD_TIMEOUT        = 42;
        public const int BOARD_FAILED         = 43;
        public const int ERROR_PAUSE_ENTER    = 50;
        public const int ERROR_PAUSE_EXIT     = 51;
    }

    // ── Internal ──────────────────────────────────────────────────────────────
    private StreamOutlet outlet;
    private StreamOutlet tetOutlet;

#if UNITY_ANDROID && !UNITY_EDITOR
    // Android filters out multicast/broadcast packets to save power unless an app
    // holds a MulticastLock. LSL stream discovery is multicast-based, so without
    // this lock a remote inlet (e.g. the Python receiver) can never resolve the
    // outlet — even though the outlet is open. A high-perf WifiLock keeps the
    // radio from sleeping during the session. Both are released in OnDestroy.
    private AndroidJavaObject multicastLock;
    private AndroidJavaObject wifiLock;

    private void AcquireAndroidNetworkLocks()
    {
        try
        {
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            using var wifiManager = activity.Call<AndroidJavaObject>("getSystemService", "wifi");

            multicastLock = wifiManager.Call<AndroidJavaObject>("createMulticastLock", "ColorQuestLSL");
            multicastLock.Call("setReferenceCounted", false);
            multicastLock.Call("acquire");

            // WIFI_MODE_FULL_HIGH_PERF = 3
            wifiLock = wifiManager.Call<AndroidJavaObject>("createWifiLock", 3, "ColorQuestLSLWifi");
            wifiLock.Call("setReferenceCounted", false);
            wifiLock.Call("acquire");

            Debug.Log("[LSLMarkerSender] Android multicast + wifi locks acquired.");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LSLMarkerSender] Could not acquire Android network locks: {e.Message}\n" +
                             "LSL discovery may fail on-device if WiFi drops multicast packets.");
        }
    }

    private void ReleaseAndroidNetworkLocks()
    {
        try { if (multicastLock != null) { multicastLock.Call("release"); multicastLock.Dispose(); multicastLock = null; } } catch { }
        try { if (wifiLock != null)      { wifiLock.Call("release");      wifiLock.Dispose();      wifiLock = null; } }      catch { }
    }
#endif

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }

        if (!markersEnabled) return;

#if UNITY_ANDROID && !UNITY_EDITOR
        AcquireAndroidNetworkLocks();
#endif
        try
        {
            // channel_count=1, nominal_srate=0 (irregular rate), cf_string
            var info = new StreamInfo(
                streamName, "Markers", 1, 0.0,
                channel_format_t.cf_string, "colorquest-adhd-v1");
            outlet = new StreamOutlet(info);
            Debug.Log($"[LSLMarkerSender] LSL outlet '{streamName}' open — visible on local network.");

            var tetInfo = new StreamInfo(
                tetStreamName, "Markers", 1, 0.0,
                channel_format_t.cf_string, tetSourceId);
            tetOutlet = new StreamOutlet(tetInfo);
            Debug.Log($"[LSLMarkerSender] LSL outlet '{tetStreamName}' open (TET).");
        }
        catch (Exception e)
        {
            Debug.LogError($"[LSLMarkerSender] Could not open LSL outlet: {e.Message}\n" +
                           "Check: LSL4Unity installed? Android arm64-v8a liblsl.so present?");
            markersEnabled = false;
        }
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
#if UNITY_ANDROID && !UNITY_EDITOR
        ReleaseAndroidNetworkLocks();
#endif
    }

    // ── Core send ──────────────────────────────────────────────────────────────
    /// <summary>
    /// Push one string marker to the LSL outlet. liblsl timestamps the sample
    /// with its internal synchronized clock and delivers it to all connected inlets.
    /// </summary>
    public void Send(int code, string label, string extra = null)
    {
        if (!markersEnabled || outlet == null) return;
        try
        {
            string marker = extra != null
                ? $"{code}|{label}|{extra}"
                : $"{code}|{label}";
            outlet.push_sample(new[] { marker });
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LSLMarkerSender] push_sample error: {e.Message}");
        }
    }

    /// <summary>Current LSL clock (seconds), the time base of every marker.</summary>
    public double LocalClock() => LSL.LSL.local_clock();

    /// <summary>
    /// Push one plain-label marker to the TET stream (GAME-SPECS §5). Pass an
    /// explicit LSL timestamp to stamp several markers with the same instant
    /// (e.g. "sync_1" and "inicio partida"); 0 = stamp now.
    /// </summary>
    public void SendTet(string label, double lslTimestamp = 0.0)
    {
        if (!markersEnabled || tetOutlet == null) return;
        try
        {
            tetOutlet.push_sample(new[] { label }, lslTimestamp);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LSLMarkerSender] TET push_sample error: {e.Message}");
        }
    }

    // ── Convenience methods — one per ADHD-relevant event ────────────────────

    /// <summary>Recording epoch boundary. Sync reference for EEG analysis.</summary>
    public void SendSessionStart() =>
        Send(Codes.SESSION_START, "session_start");

    public void SendSessionEnd() =>
        Send(Codes.SESSION_END, "session_end");

    /// <summary>
    /// Level transition. Code encodes load level (21-25) so EEG epochs can be
    /// segmented by working-memory demand without parsing the label string.
    /// </summary>
    public void SendLevelStart(int level) =>
        Send(Codes.LEVEL_BASE + level, $"level_start_{level}", $"level={level}");

    /// <summary>Trial onset — use as epoch anchor in EEG analysis.</summary>
    public void SendBoardSpawn(int boardId, int level, int gridSize) =>
        Send(Codes.BOARD_SPAWN, "board_spawn",
             $"board={boardId},level={level},grid={gridSize}");

    /// <summary>
    /// Stimulus presentation (sequence made visible).
    /// P300 component of interest for working-memory encoding.
    /// </summary>
    public void SendSequenceShown(int level) =>
        Send(Codes.SEQUENCE_SHOWN, "sequence_shown", $"level={level}");

    /// <summary>
    /// Correct response. rt_board = reaction time from trial onset (ms).
    /// rt_prev = inter-response interval (ms). Key metric for sustained attention.
    /// </summary>
    public void SendSphereHitCorrect(string sphereId, int positionInSequence,
                                      long rtSinceBoardSpawnMs, long rtSincePrevHitMs) =>
        Send(Codes.SPHERE_HIT_CORRECT, "sphere_hit_correct",
             $"id={sphereId},pos={positionInSequence}," +
             $"rt_board={rtSinceBoardSpawnMs},rt_prev={rtSincePrevHitMs}");

    /// <summary>
    /// Commission error (wrong sphere). Maps to impulsivity / inhibitory control.
    /// ERP of interest: N2 and error-related negativity (ERN).
    /// </summary>
    public void SendSphereHitIncorrect(string clickedId, string expectedId,
                                        int positionInSequence, long rtMs) =>
        Send(Codes.SPHERE_HIT_INCORRECT, "sphere_hit_incorrect",
             $"clicked={clickedId},expected={expectedId},pos={positionInSequence},rt={rtMs}");

    /// <summary>
    /// Omission error (board expired). Maps to inattention / vigilance decrement.
    /// Key measure for ADHD-inattentive subtype.
    /// </summary>
    public void SendBoardTimeout(int targetsHitBeforeTimeout) =>
        Send(Codes.BOARD_TIMEOUT, "board_timeout",
             $"partial_hits={targetsHitBeforeTimeout}");

    public void SendBoardFailed(string clickedId, string expectedId, long rtMs) =>
        Send(Codes.BOARD_FAILED, "board_failed",
             $"clicked={clickedId},expected={expectedId},rt={rtMs}");

    /// <summary>Start of inhibition pause. Post-error slowing is a core ADHD biomarker.</summary>
    public void SendErrorPauseEnter(string clickedId, string expectedId) =>
        Send(Codes.ERROR_PAUSE_ENTER, "error_pause_enter",
             $"clicked={clickedId},expected={expectedId}");

    public void SendErrorPauseExit(long pauseDurationMs) =>
        Send(Codes.ERROR_PAUSE_EXIT, "error_pause_exit",
             $"dur_ms={pauseDurationMs}");
}

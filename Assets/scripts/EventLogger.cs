using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Append-only, newline-delimited JSON (NDJSON) event logger for the ADHD
/// mixed-reality training game. One JSON object per line. Each event carries a
/// shared envelope (session/participant/difficulty context) plus event-specific
/// fields, so every line is self-describing for offline analysis.
///
/// Design notes:
///  - Singleton, mirrors the GameController.Instance pattern.
///  - Writes to Application.persistentDataPath (works on Quest/Android builds).
///  - Buffers in memory and flushes periodically + on quit, to avoid disk I/O
///    on every trigger pull (which would hitch VR framerate).
///  - Pure value logging: never collects names or other PII. participant_id is
///    a pseudonymized code you assign.
/// </summary>
public class EventLogger : MonoBehaviour
{
    public static EventLogger Instance { get; private set; }

    [Header("Identity (set before a session, e.g. from a config screen)")]
    [SerializeField] private string participantId = "ANON";

    [Header("Flush behaviour")]
    [Tooltip("Flush buffered events to disk every N seconds.")]
    [SerializeField] private float flushIntervalSeconds = 5f;

    // --- Session-scoped context, refreshed on StartSession() ---
    private string sessionId;
    private long sessionStartUnixMs;
    private float sessionStartRealtime;

    // --- Difficulty context, kept current via SetContext() ---
    private int level = 1;
    private int gridSize = 2;
    private int numDistractors = 0;
    private int boardId = 0;

    private readonly List<string> buffer = new List<string>();
    private string filePath;
    private float flushTimer;
    private bool sessionActive;
    private long eventSeq = 0;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            enabled = false;   // impide que Start() corra en el duplicado
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        if (!sessionActive) return;
        flushTimer += Time.unscaledDeltaTime;
        if (flushTimer >= flushIntervalSeconds)
        {
            flushTimer = 0f;
            Flush();
        }
    }

    // ===================================================================
    //  Session lifecycle
    // ===================================================================

    /// <summary>Begin a new logging session. Call once at game start.</summary>
    public void StartSession(string deviceModel, string appVersion, string controllerHand,
                             float totalLevelDurationS, float boardDurationS)
    {
        if (sessionActive) return;   // guard: nunca abrir dos archivos en la misma sesión
        sessionId = Guid.NewGuid().ToString();
        sessionStartUnixMs = NowUnixMs();
        sessionStartRealtime = Time.realtimeSinceStartup;
        sessionActive = true;

        string fileName = $"events_{participantId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.ndjson";
        filePath = Path.Combine(Application.persistentDataPath, fileName);

        var fields = new Dictionary<string, object>
        {
            { "device_model", deviceModel },
            { "app_version", appVersion },
            { "controller_hand", controllerHand },
            { "total_level_duration_s", totalLevelDurationS },
            { "board_duration_s", boardDurationS }
        };
        Log("session_start", fields);
        LSLMarkerSender.Instance?.SendSessionStart();
        Debug.Log($"[EventLogger] Session started -> {filePath}");
    }

    /// <summary>Call on scene end / quit.</summary>
    public void EndSession(int finalScore, int boardsCompleted)
    {
        if (!sessionActive) return;
        var fields = new Dictionary<string, object>
        {
            { "final_score", finalScore },
            { "boards_completed", boardsCompleted },
            { "total_duration_ms", SessionTimeMs() }
        };
        Log("session_end", fields);
        LSLMarkerSender.Instance?.SendSessionEnd();
        Flush();
        sessionActive = false;
    }

    /// <summary>
    /// Update the difficulty/board context. Call from EntrarANivel() and
    /// whenever a new board spawns so subsequent events are tagged correctly.
    /// </summary>
    public void SetContext(int level, int gridSize, int numDistractors)
    {
        this.level = level;
        this.gridSize = gridSize;
        this.numDistractors = numDistractors;
    }

    /// <summary>Increment and return the board id (call on each new board).</summary>
    public int NextBoardId()
    {
        boardId++;
        return boardId;
    }

    /// <summary>Return the current board id without incrementing.</summary>
    public int GetBoardId() => boardId;

    // ===================================================================
    //  Event helpers (one per event_type)
    // ===================================================================

    public void LevelStart()
    {
        LSLMarkerSender.Instance?.SendLevelStart(level);
        LSLMarkerSender.Instance?.SendTet($"inicio nivel {level}");
        Log("level_start", null);
    }

    public void BoardSpawn(IReadOnlyList<string> targetSequence,
                           IReadOnlyList<string> distractorIds,
                           IReadOnlyList<Vector3> spherePositions,
                           float boardDurationS)
    {
        var fields = new Dictionary<string, object>
        {
            { "target_sequence", new List<string>(targetSequence) },
            { "distractor_ids", new List<string>(distractorIds) },
            { "sphere_positions", Vec3List(spherePositions) },
            { "board_duration_s", boardDurationS }
        };
        LSLMarkerSender.Instance?.SendBoardSpawn(boardId, level, gridSize);
        Log("board_spawn", fields);
    }

    public void SequenceShown(IReadOnlyList<string> targetSequence)
    {
        var fields = new Dictionary<string, object>
        {
            { "target_sequence", new List<string>(targetSequence) }
        };
        LSLMarkerSender.Instance?.SendSequenceShown(level);
        Log("sequence_shown", fields);
    }

    /// <summary>
    /// Aiming context of one shot, measured by Blaster just before it is resolved.
    /// Angles in degrees. "Edge" angle = angle from the ray to the sphere's
    /// silhouette (≤ 0: the ray passes through the sphere). Null = no such sphere.
    /// </summary>
    public struct AimInfo
    {
        public string controller;            // "RTouch" | "LTouch"
        public float barrelPitchDeg;         // fixed barrel offset (Blaster.anguloCanon)
        public string hoveredSphereId;       // sphere under the laser when the trigger crossed the threshold
        public long hoverDwellMs;            // time the laser had been on that sphere
        public string hoveredAtPressStartId; // sphere under the laser when the trigger started moving (≥ 0.05)
        public string nearestSphereId;       // sphere with the smallest edge angle
        public float? nearestCenterAngleDeg;
        public float? nearestEdgeAngleDeg;
        public string expectedSphereId;      // sphere the sequence asked for
        public float? expectedEdgeAngleDeg;
        public float nearMissThresholdDeg;
    }

    public void TriggerPull(bool hit, string hitType, string targetObjectId,
                            Vector3 aimOrigin, Vector3 aimDirection,
                            long rtSinceBoardSpawnMs, AimInfo aim)
    {
        if (rtSinceBoardSpawnMs < 50) return;   // ignorar disparos de arrastre entre tableros
        // Near miss: no sphere was hit but the ray passed within the threshold of one
        // (motor error, as opposed to a commission error on the wrong sphere).
        bool nearMiss = hitType != "sphere" && aim.nearestEdgeAngleDeg.HasValue
                        && aim.nearestEdgeAngleDeg.Value <= aim.nearMissThresholdDeg;
        var fields = new Dictionary<string, object>
        {
            { "hit", hit },
            { "hit_type", hitType },                 // "sphere" | "grid" | "miss"
            { "target_object_id", targetObjectId },
            { "aim_origin",    new RawJson(Vec3(aimOrigin))    },
            { "aim_direction", new RawJson(Vec3(aimDirection)) },
            { "rt_since_board_spawn_ms", rtSinceBoardSpawnMs },
            { "controller", aim.controller },
            { "barrel_pitch_deg", aim.barrelPitchDeg },
            { "hovered_sphere_id", aim.hoveredSphereId },
            { "hover_dwell_ms", aim.hoverDwellMs },
            { "hovered_at_press_start_id", aim.hoveredAtPressStartId },
            { "nearest_sphere_id", aim.nearestSphereId },
            { "nearest_center_angle_deg", aim.nearestCenterAngleDeg },
            { "nearest_edge_angle_deg", aim.nearestEdgeAngleDeg },
            { "expected_sphere_id", aim.expectedSphereId },
            { "expected_edge_angle_deg", aim.expectedEdgeAngleDeg },
            { "near_miss", nearMiss },
            { "near_miss_threshold_deg", aim.nearMissThresholdDeg }
        };
        Log("trigger_pull", fields);
    }

    /// <summary>
    /// The aim laser entered a board sphere. If haptic is true a vibration pulse
    /// with these parameters started at this instant: a tactile stimulus, so it is
    /// also sent as an LSL marker to align it with the EEG. Re-entering the same
    /// sphere within refractoryMs of leaving it is treated as the same hover and
    /// is not logged again (the laser grazing the sphere's edge).
    /// </summary>
    public void SphereHover(string sphereId, string controller, bool haptic,
                            float hapticAmplitude, float hapticFrequency, long hapticDurationMs,
                            long refractoryMs)
    {
        var fields = new Dictionary<string, object>
        {
            { "sphere_id", sphereId },
            { "controller", controller },
            { "haptic", haptic },
            { "haptic_amplitude", haptic ? hapticAmplitude : 0f },
            { "haptic_frequency", haptic ? hapticFrequency : 0f },
            { "haptic_duration_ms", haptic ? hapticDurationMs : 0L },
            { "refractory_ms", refractoryMs }
        };
        LSLMarkerSender.Instance?.SendSphereHover(sphereId, controller, haptic);
        Log("sphere_hover", fields);
    }

    public void SphereHitCorrect(string sphereId, int positionInSequence, string expectedId,
                                 long rtSinceBoardSpawnMs, long rtSincePrevHitMs, int currentIndex)
    {
        var fields = new Dictionary<string, object>
        {
            { "sphere_id", sphereId },
            { "position_in_sequence", positionInSequence },
            { "expected_id", expectedId },
            { "rt_since_board_spawn_ms", rtSinceBoardSpawnMs },
            { "rt_since_prev_hit_ms", rtSincePrevHitMs },
            { "current_index", currentIndex }
        };
        LSLMarkerSender.Instance?.SendSphereHitCorrect(sphereId, positionInSequence, rtSinceBoardSpawnMs, rtSincePrevHitMs);
        LSLMarkerSender.Instance?.SendTet($"acierto objetivo {positionInSequence + 1}");
        Log("sphere_hit_correct", fields);
    }

    public void SphereHitIncorrect(string clickedId, string expectedId, int positionInSequence,
                                   long rtMs)
    {
        var fields = new Dictionary<string, object>
        {
            { "clicked_id", clickedId },
            { "expected_id", expectedId },
            { "position_in_sequence", positionInSequence },
            { "rt_ms", rtMs }
        };
        LSLMarkerSender.Instance?.SendSphereHitIncorrect(clickedId, expectedId, positionInSequence, rtMs);
        LSLMarkerSender.Instance?.SendTet($"error objetivo {positionInSequence + 1}");
        Log("sphere_hit_incorrect", fields);
    }

    public void SequenceCompleted(int pointsAwarded, int totalScore, long completionTimeMs,
                                  int errorsThisBoard)
    {
        var fields = new Dictionary<string, object>
        {
            { "points_awarded", pointsAwarded },
            { "total_score", totalScore },
            { "completion_time_ms", completionTimeMs },
            { "errors_this_board", errorsThisBoard }
        };
        LSLMarkerSender.Instance?.SendTet($"premio puntos {pointsAwarded}");
        Log("sequence_completed", fields);
    }

    public void BoardTimeout(int targetsHitBeforeTimeout, float partialProgress)
    {
        var fields = new Dictionary<string, object>
        {
            { "targets_hit_before_timeout", targetsHitBeforeTimeout },
            { "partial_progress", partialProgress }
        };
        LSLMarkerSender.Instance?.SendBoardTimeout(targetsHitBeforeTimeout);
        Log("board_timeout", fields);
    }

    public void BoardFailed(int targetsHitBeforeError, string clickedId, string expectedId, long rtMs)
    {
        var fields = new Dictionary<string, object>
        {
            { "targets_hit_before_error", targetsHitBeforeError },
            { "clicked_id", clickedId },
            { "expected_id", expectedId },
            { "rt_ms", rtMs }
        };
        LSLMarkerSender.Instance?.SendBoardFailed(clickedId, expectedId, rtMs);
        Log("board_failed", fields);
    }

    public void ErrorPauseEnter(string triggeringClickedId, string triggeringExpectedId)
    {
        var fields = new Dictionary<string, object>
        {
            { "triggering_clicked_id", triggeringClickedId },
            { "triggering_expected_id", triggeringExpectedId }
        };
        LSLMarkerSender.Instance?.SendErrorPauseEnter(triggeringClickedId, triggeringExpectedId);
        Log("error_pause_enter", fields);
    }

    public void ErrorPauseExit(long pauseDurationMs)
    {
        var fields = new Dictionary<string, object>
        {
            { "pause_duration_ms", pauseDurationMs }
        };
        LSLMarkerSender.Instance?.SendErrorPauseExit(pauseDurationMs);
        Log("error_pause_exit", fields);
    }

    public void HapticFeedback(float intensity, long durationMs, string reason)
    {
        var fields = new Dictionary<string, object>
        {
            { "intensity", intensity },
            { "duration_ms", durationMs },
            { "reason", reason }
        };
        Log("haptic_feedback", fields);
    }

    /// <summary>
    /// TET sync flash k shown (GAME-SPECS §3). lsl_time is the LSL timestamp
    /// pushed with the "sync_k" marker, so the NDJSON can be cross-checked
    /// against the XDF.
    /// </summary>
    public void SyncFlash(int index, double lslTime)
    {
        var fields = new Dictionary<string, object>
        {
            { "sync_index", index },
            { "label", $"sync_{index}" },
            { "lsl_time", lslTime }
        };
        Log("sync_flash", fields);
    }

    /// <summary>
    /// Aiming-practice summary (per-participant aiming baseline). Practice runs
    /// before the session opens, so this is logged right after session_start.
    /// No LSL marker: practice is not part of the EEG-locked task.
    /// </summary>
    public void PracticeSummary(float durationS, int gridSize, int targetsShown,
                                int shots, int hits, long meanTimeToHitMs)
    {
        var fields = new Dictionary<string, object>
        {
            { "practice_duration_s", durationS },
            { "practice_grid_size", gridSize },
            { "targets_shown", targetsShown },
            { "shots", shots },
            { "hits", hits },
            { "accuracy", shots > 0 ? (float)hits / shots : 0f },
            { "mean_time_to_hit_ms", meanTimeToHitMs }
        };
        Log("practice_summary", fields);
    }

    /// <summary>
    /// Time the welcome panel was visible before the player shot the practice button.
    /// Logged right after session_start (practice happens before the session opens).
    /// </summary>
    public void WelcomeViewed(long visibleMs)
    {
        var fields = new Dictionary<string, object>
        {
            { "visible_ms", visibleMs }
        };
        Log("welcome_viewed", fields);
    }

    /// <summary>
    /// Time the instructions panel was visible before the player shot INICIAR.
    /// Logged right after session_start (the panel is shown before the session opens).
    /// </summary>
    public void InstructionsViewed(long visibleMs)
    {
        var fields = new Dictionary<string, object>
        {
            { "visible_ms", visibleMs }
        };
        Log("instructions_viewed", fields);
    }

    public void ScoreUpdate(int oldScore, int newScore)
    {
        var fields = new Dictionary<string, object>
        {
            { "old_score", oldScore },
            { "new_score", newScore },
            { "delta", newScore - oldScore }
        };
        Log("score_update", fields);
    }

    // ===================================================================
    //  Core: build envelope + serialize + buffer
    // ===================================================================

    private void Log(string eventType, Dictionary<string, object> eventFields)
    {
        if (!sessionActive) return;
        var sb = new StringBuilder(256);
        sb.Append('{');

        // --- shared envelope ---
        AppendString(sb, "event_id", $"{eventType}_{NowUnixMs()}_{Guid.NewGuid().ToString().Substring(0, 8)}"); sb.Append(',');
        AppendRaw(sb, "seq", (eventSeq++).ToString()); sb.Append(',');
        AppendString(sb, "event_type", eventType); sb.Append(',');
        AppendRaw(sb, "timestamp_unix_ms", NowUnixMs().ToString()); sb.Append(',');
        AppendRaw(sb, "session_time_ms", SessionTimeMs().ToString()); sb.Append(',');
        AppendString(sb, "session_id", sessionId ?? ""); sb.Append(',');
        AppendString(sb, "participant_id", participantId); sb.Append(',');
        AppendRaw(sb, "level", level.ToString()); sb.Append(',');
        AppendRaw(sb, "grid_size", gridSize.ToString()); sb.Append(',');
        AppendRaw(sb, "num_distractors", numDistractors.ToString()); sb.Append(',');
        AppendRaw(sb, "board_id", boardId.ToString());

        // --- event-specific fields ---
        if (eventFields != null)
        {
            foreach (var kv in eventFields)
            {
                sb.Append(',');
                AppendValue(sb, kv.Key, kv.Value);
            }
        }

        sb.Append('}');
        buffer.Add(sb.ToString());
    }

    private void Flush()
    {
        if (buffer.Count == 0 || string.IsNullOrEmpty(filePath)) return;
        try
        {
            File.AppendAllLines(filePath, buffer);
            buffer.Clear();
        }
        catch (Exception e)
        {
            Debug.LogError($"[EventLogger] Flush failed: {e.Message}");
        }
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) Flush();   // Quest backgrounding fires this; don't lose data
    }

    private void OnApplicationQuit()
    {
        Flush();
    }

    // ===================================================================
    //  Serialization helpers (hand-rolled to avoid pulling in a JSON lib
    //  and to keep numeric types un-stringified for analysis)
    // ===================================================================

    private static long NowUnixMs()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    private long SessionTimeMs()
    {
        return (long)((Time.realtimeSinceStartup - sessionStartRealtime) * 1000f);
    }

    private static void AppendString(StringBuilder sb, string key, string value)
    {
        sb.Append('"').Append(key).Append("\":\"").Append(Escape(value)).Append('"');
    }

    private static void AppendRaw(StringBuilder sb, string key, string rawValue)
    {
        sb.Append('"').Append(key).Append("\":").Append(rawValue);
    }

    private static void AppendValue(StringBuilder sb, string key, object value)
    {
        sb.Append('"').Append(key).Append("\":");
        sb.Append(ValueToJson(value));
    }

    private static string ValueToJson(object value)
    {
        switch (value)
        {
            case null:
                return "null";
            case RawJson rj:
                return rj.Json;
            case bool b:
                return b ? "true" : "false";
            case int i:
                return i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            case long l:
                return l.ToString(System.Globalization.CultureInfo.InvariantCulture);
            case float f:
                return f.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
            case double d:
                return d.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
            case string s:
                return "\"" + Escape(s) + "\"";
            case List<string> ls:
                {
                    var sb = new StringBuilder("[");
                    for (int k = 0; k < ls.Count; k++)
                    {
                        if (k > 0) sb.Append(',');
                        sb.Append('"').Append(Escape(ls[k])).Append('"');
                    }
                    return sb.Append(']').ToString();
                }
            case List<object> lo:
                {
                    var sb = new StringBuilder("[");
                    for (int k = 0; k < lo.Count; k++)
                    {
                        if (k > 0) sb.Append(',');
                        sb.Append(ValueToJson(lo[k]));
                    }
                    return sb.Append(']').ToString();
                }
            default:
                return "\"" + Escape(value.ToString()) + "\"";
        }
    }

    /// <summary>Serialize a Vector3 as a {x,y,z} object.</summary>
    private static string Vec3(Vector3 v)
    {
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        return "{\"x\":" + v.x.ToString("0.####", ci) +
               ",\"y\":" + v.y.ToString("0.####", ci) +
               ",\"z\":" + v.z.ToString("0.####", ci) + "}";
    }

    /// <summary>Serialize a list of Vector3 as a JSON array of {x,y,z} objects.</summary>
    private static RawJson Vec3List(IReadOnlyList<Vector3> verts)
    {
        var sb = new StringBuilder("[");
        for (int k = 0; k < verts.Count; k++)
        {
            if (k > 0) sb.Append(',');
            sb.Append(Vec3(verts[k]));
        }
        sb.Append(']');
        return new RawJson(sb.ToString());
    }

    private static string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length + 8);
        foreach (char c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }

    /// <summary>Wrapper so already-serialized JSON is emitted verbatim.</summary>
    private sealed class RawJson
    {
        public readonly string Json;
        public RawJson(string json) { Json = json; }
        public override string ToString() => Json;
    }
}

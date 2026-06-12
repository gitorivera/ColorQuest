using System;
using System.Net.Sockets;
using UnityEngine;

/// <summary>
/// UDP event-marker sender for neurocognitive ADHD research.
/// Sends fixed 11-byte packets from the Quest to a PC receiver
/// (Python, MATLAB, Lab Streaming Layer bridge, etc.) for
/// EEG / physiological signal synchronisation.
///
/// Packet layout (little-endian):
///   [0]      marker_code    : byte   – event type (see constants below)
///   [1]      level          : byte   – current game level (1-based)
///   [2]      grid_size      : byte   – N×N grid dimension
///   [3]      num_distractors: byte   – distractor count on current board
///   [4–5]    board_id       : uint16 – board counter within session
///   [6–9]    rt_ms          : int32  – reaction time in ms (-1 = not applicable)
///   [10]     extra          : byte   – event-specific payload (documented per method)
///
/// Python one-liner to decode:
///   import struct
///   code, lvl, gs, nd, bid, rt, ex = struct.unpack_from('<BBBBHiB', data)
/// </summary>
public class UdpEventSender : MonoBehaviour
{
    public static UdpEventSender Instance { get; private set; }

    [Header("Network")]
    [SerializeField] private string hostIP = "192.168.1.100";
    [SerializeField] private int port = 9999;

    [Header("Vigilance thresholds (ms)")]
    [Tooltip("RT above this value emits a SLOW_RESPONSE marker (vigilance lapse indicator).")]
    [SerializeField] private int slowResponseThresholdMs = 1500;
    [Tooltip("RT below this value emits a FAST_RESPONSE marker (impulsivity indicator).")]
    [SerializeField] private int fastResponseThresholdMs = 200;

    // =====================================================================
    //  Marker codes — single byte, grouped by cognitive domain
    //  Range layout mirrors parallel-port EEG trigger conventions.
    //  The code byte is sent verbatim to the BioSemi trigger box.
    // =====================================================================

    // Synchronisation / calibration  (0x01–0x0F)
    public const byte SYNC_PULSE            = 0x01; // PC↔Quest timing calibration pulse

    // Session lifecycle  (0x10–0x1F)
    public const byte SESSION_START         = 0x10; // Resting-state / baseline onset
    public const byte SESSION_END           = 0x11; // Session offset

    // Difficulty / context change  (0x20–0x2F)
    public const byte LEVEL_START           = 0x20; // New difficulty block onset
    public const byte LEVEL_END             = 0x21; // Difficulty block offset
    public const byte DIFFICULTY_CHANGE     = 0x22; // Adaptive mid-session adjustment

    // Board (trial) lifecycle  (0x30–0x3F)
    public const byte BOARD_SPAWN           = 0x30; // Trial onset — stimuli appear
    public const byte BOARD_TIMEOUT         = 0x31; // Omission / sustained-attention failure
    public const byte BOARD_FAILED          = 0x32; // Commission / inhibitory control failure

    // Working memory  (0x40–0x4F)
    public const byte SEQUENCE_SHOWN        = 0x40; // Encoding phase onset — WM load begins
    public const byte SEQUENCE_COMPLETED    = 0x41; // Retrieval success — WM load resolved

    // Response accuracy — core ADHD metrics  (0x50–0x5F)
    public const byte SPHERE_CORRECT        = 0x50; // True positive — correct target hit
    public const byte SPHERE_INCORRECT      = 0x51; // Commission error — wrong target hit
    public const byte DISTRACTOR_HIT        = 0x52; // Inhibitory failure — distractor selected
    public const byte TARGET_MISSED         = 0x53; // Omission error — required target not hit

    // Raw motor event  (0x60–0x6F)
    public const byte TRIGGER_PULL          = 0x60; // Any trigger press (pre-classification)

    // Error monitoring & post-error adjustment  (0x70–0x7F)
    public const byte ERROR_FEEDBACK_ONSET  = 0x70; // Error feedback begins (red grid shown)
    public const byte ERROR_FEEDBACK_OFFSET = 0x71; // Error feedback ends, task resumes

    // Reward / motivation processing  (0x80–0x8F)
    public const byte SCORE_INCREASE        = 0x80; // Positive reward signal delivered
    public const byte SCORE_PENALTY         = 0x81; // Negative feedback / point loss
    public const byte HAPTIC_FEEDBACK       = 0x82; // Tactile reinforcement marker

    // Vigilance & intra-individual variability  (0x90–0x9F)
    public const byte SLOW_RESPONSE         = 0x90; // RT > slowResponseThresholdMs (lapse?)
    public const byte FAST_RESPONSE         = 0x91; // RT < fastResponseThresholdMs (impulsive?)

    // =====================================================================

    private const int PACKET_SIZE = 11;

    private UdpClient udp;
    private byte _level          = 1;
    private byte _gridSize       = 2;
    private byte _numDistractors = 0;
    private ushort _boardId      = 0;

    // =====================================================================
    //  Unity lifecycle
    // =====================================================================

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            udp = new UdpClient();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        udp?.Close();
    }

    // =====================================================================
    //  Context sync — call alongside EventLogger.SetContext / NextBoardId
    // =====================================================================

    /// <summary>
    /// Mirror the current difficulty state so every packet carries correct context.
    /// Call together with EventLogger.Instance.SetContext().
    /// </summary>
    public void SetContext(int level, int gridSize, int numDistractors)
    {
        _level          = (byte)Mathf.Clamp(level,          0, 255);
        _gridSize       = (byte)Mathf.Clamp(gridSize,       0, 255);
        _numDistractors = (byte)Mathf.Clamp(numDistractors, 0, 255);
    }

    /// <summary>
    /// Advance the board counter. Call together with EventLogger.Instance.NextBoardId().
    /// </summary>
    public void SetBoardId(int boardId)
    {
        _boardId = (ushort)Mathf.Clamp(boardId, 0, ushort.MaxValue);
    }

    // =====================================================================
    //  Convenience methods — mirror EventLogger API
    // =====================================================================

    /// <summary>
    /// Send a timing-calibration pulse before the session.
    /// Call this a few times from the PC side via a test button to measure
    /// round-trip latency and verify the serial trigger fires correctly.
    /// </summary>
    public void SendSyncPulse()             => Send(SYNC_PULSE);

    public void SendSessionStart()          => Send(SESSION_START);
    public void SendSessionEnd()            => Send(SESSION_END);
    public void SendLevelStart()            => Send(LEVEL_START);
    public void SendLevelEnd()              => Send(LEVEL_END);
    public void SendBoardSpawn()            => Send(BOARD_SPAWN);
    public void SendSequenceShown()         => Send(SEQUENCE_SHOWN);
    public void SendErrorFeedbackOnset()    => Send(ERROR_FEEDBACK_ONSET);
    public void SendErrorFeedbackOffset()   => Send(ERROR_FEEDBACK_OFFSET);
    public void SendHapticFeedback()        => Send(HAPTIC_FEEDBACK);

    /// <summary>
    /// Board ended by timeout (omission / sustained-attention failure).
    /// extra = targets_hit_before_timeout (partial progress, 0–255).
    /// </summary>
    public void SendBoardTimeout(int targetsHitBeforeTimeout = 0)
        => Send(BOARD_TIMEOUT,
                extra: (byte)Mathf.Clamp(targetsHitBeforeTimeout, 0, 255));

    /// <summary>
    /// Board ended by commission error (inhibitory control failure).
    /// extra = targets_hit_before_error (partial progress, 0–255).
    /// </summary>
    public void SendBoardFailed(int targetsHitBeforeError = 0)
        => Send(BOARD_FAILED,
                extra: (byte)Mathf.Clamp(targetsHitBeforeError, 0, 255));

    /// <summary>
    /// Working-memory retrieval success.
    /// extra = errors_this_board (total commission errors, 0–255).
    /// </summary>
    public void SendSequenceCompleted(int errorsThisBoard = 0)
        => Send(SEQUENCE_COMPLETED,
                extra: (byte)Mathf.Clamp(errorsThisBoard, 0, 255));

    /// <summary>
    /// Correct sphere hit. Carries RT and sequence position.
    /// Also auto-emits SLOW_RESPONSE / FAST_RESPONSE vigilance markers if RT is out of range.
    /// extra = positionInSequence (0-based, 0–255).
    /// </summary>
    public void SendSphereCorrect(long rtMs, int positionInSequence = 0)
    {
        Send(SPHERE_CORRECT,
             rtMs:  rtMs,
             extra: (byte)Mathf.Clamp(positionInSequence, 0, 255));
        CheckRtThresholds(rtMs);
    }

    /// <summary>
    /// Commission error: player hit the wrong target sphere.
    /// extra = positionInSequence attempted (0-based, 0–255).
    /// </summary>
    public void SendSphereIncorrect(long rtMs, int positionInSequence = 0)
        => Send(SPHERE_INCORRECT,
                rtMs:  rtMs,
                extra: (byte)Mathf.Clamp(positionInSequence, 0, 255));

    /// <summary>
    /// Inhibitory control failure: player hit a distractor sphere.
    /// </summary>
    public void SendDistractorHit(long rtMs)
        => Send(DISTRACTOR_HIT, rtMs: rtMs);

    /// <summary>
    /// Omission error: board ended without the player hitting a required target.
    /// extra = targets_hit_before_timeout (0–255).
    /// </summary>
    public void SendTargetMissed(int targetsHitBeforeTimeout = 0)
        => Send(TARGET_MISSED,
                extra: (byte)Mathf.Clamp(targetsHitBeforeTimeout, 0, 255));

    /// <summary>
    /// Raw trigger press before hit classification.
    /// extra = 1 if a sphere was hit, 0 otherwise.
    /// </summary>
    public void SendTriggerPull(bool hit, long rtMs)
        => Send(TRIGGER_PULL, rtMs: rtMs, extra: hit ? (byte)1 : (byte)0);

    /// <summary>
    /// Score change. Automatically selects SCORE_INCREASE or SCORE_PENALTY.
    /// extra = abs(delta) capped at 255.
    /// </summary>
    public void SendScoreUpdate(int delta)
    {
        byte code     = delta >= 0 ? SCORE_INCREASE : SCORE_PENALTY;
        byte absDelta = (byte)Mathf.Clamp(Mathf.Abs(delta), 0, 255);
        Send(code, extra: absDelta);
    }

    // =====================================================================
    //  Core send — build packet and dispatch
    // =====================================================================

    /// <summary>Build and send a fixed 11-byte marker packet.</summary>
    public void Send(byte code, long rtMs = -1, byte extra = 0)
    {
        if (udp == null) return;
        try
        {
            byte[] packet = BuildPacket(code, rtMs, extra);
            udp.Send(packet, packet.Length, hostIP, port);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[UDP] 0x{code:X2}  lvl={_level}  grid={_gridSize}  dist={_numDistractors}  board={_boardId}  rt={rtMs}  extra={extra}");
#endif
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[UDP] Send failed: {e.Message}");
        }
    }

    // =====================================================================
    //  Internals
    // =====================================================================

    private byte[] BuildPacket(byte code, long rtMs, byte extra)
    {
        byte[] p = new byte[PACKET_SIZE];
        p[0] = code;
        p[1] = _level;
        p[2] = _gridSize;
        p[3] = _numDistractors;
        p[4] = (byte)( _boardId        & 0xFF);
        p[5] = (byte)((_boardId >> 8)  & 0xFF);
        int rt = (int)Math.Max(int.MinValue, Math.Min((long)int.MaxValue, rtMs));
        p[6]  = (byte)( rt         & 0xFF);
        p[7]  = (byte)((rt >>  8)  & 0xFF);
        p[8]  = (byte)((rt >> 16)  & 0xFF);
        p[9]  = (byte)((rt >> 24)  & 0xFF);
        p[10] = extra;
        return p;
    }

    private void CheckRtThresholds(long rtMs)
    {
        if (rtMs > slowResponseThresholdMs)
            Send(SLOW_RESPONSE, rtMs: rtMs);
        else if (rtMs > 0 && rtMs < fastResponseThresholdMs)
            Send(FAST_RESPONSE, rtMs: rtMs);
    }
}

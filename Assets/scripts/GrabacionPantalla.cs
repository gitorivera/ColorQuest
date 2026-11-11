using UnityEngine;

/// <summary>
/// Puente con el plugin Android GrabadorPantalla.java (sólo tablet): graba la pantalla
/// durante la partida para la app TET (GAME-SPECS §6.2). La grabación empieza al abrir
/// la app, tras aceptar el diálogo del sistema, y se detiene justo antes de cerrarla, así
/// que contiene todos los destellos sync_k.
///
/// El video es un MP4 H.264 sin audio en Application.persistentDataPath, junto al NDJSON
/// (/sdcard/Android/data/com.UTP.ColorQuestTablet/files/). En el editor no hace nada.
///
/// GameController la crea con Asegurar() en Start() y registra el estado con
/// EventLogger.ScreenRecording() al abrir la sesión y ScreenRecordingStop() al salir.
/// </summary>
public class GrabacionPantalla : MonoBehaviour
{
    public static GrabacionPantalla Instance { get; private set; }

    // Estados del plugin (GrabadorPantalla.java)
    public const string Inactivo = "inactivo";
    public const string EsperandoPermiso = "esperando_permiso";
    public const string Grabando = "grabando";
    public const string Rechazado = "rechazado";
    public const string Detenido = "detenido";
    public const string Error = "error";
    public const string NoDisponible = "no_disponible";   // editor u otra plataforma

    private const int CuadrosPorSegundo = 30;
    private const int BitsPorSegundo = 6_000_000;

    public string Archivo { get; private set; } = "";

#if UNITY_ANDROID && !UNITY_EDITOR
    private const string ClaseJava = "com.utp.colorquest.grabacion.GrabadorPantalla";
    private AndroidJavaClass plugin;
#endif

    public static GrabacionPantalla Asegurar()
    {
        if (Instance == null)
        {
            var go = new GameObject("GrabacionPantalla");
            DontDestroyOnLoad(go);
            go.AddComponent<GrabacionPantalla>();
        }
        return Instance;
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { enabled = false; Destroy(gameObject); }
    }

    /// <summary>Muestra el diálogo del sistema y, si se acepta, empieza a grabar.</summary>
    public void Solicitar(string participantId)
    {
        Archivo = $"screen_{participantId}_{System.DateTime.UtcNow:yyyyMMdd_HHmmss}.mp4";
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            plugin ??= new AndroidJavaClass(ClaseJava);
            string ruta = System.IO.Path.Combine(Application.persistentDataPath, Archivo);
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var actividad = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            plugin.CallStatic("solicitar", actividad, ruta, CuadrosPorSegundo, BitsPorSegundo);
            Debug.Log($"[GrabacionPantalla] Permiso pedido; destino {ruta}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GrabacionPantalla] No se pudo pedir la grabación: {e.Message}");
        }
#else
        Debug.Log("[GrabacionPantalla] Sin grabación de pantalla fuera de Android");
#endif
    }

    public string Estado
    {
        get
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { return plugin != null ? plugin.CallStatic<string>("estado") : Inactivo; }
            catch (System.Exception) { return Error; }
#else
            return NoDisponible;
#endif
        }
    }

    public bool EstaGrabando => Estado == Grabando;

    public string MensajeError => LlamarString("error");

    public int Ancho => LlamarInt("ancho");
    public int Alto => LlamarInt("alto");
    public int Fps => LlamarInt("fps");
    public int Bitrate => LlamarInt("bitrate");

    /// <summary>
    /// Tiempo LSL en que arrancó el grabador (null si no arrancó). Es aproximado: el primer
    /// cuadro llega unas decenas de ms después; para alinear se usan los destellos.
    /// </summary>
    public double? InicioLsl => ASegundosLsl(LlamarLong("inicioNanos"));

    /// <summary>Tiempo LSL en que se cerró el grabador (null si no se cerró).</summary>
    public double? FinLsl => ASegundosLsl(LlamarLong("finNanos"));

    /// <summary>Detiene la grabación y cierra el MP4 (síncrono). Devuelve el estado final.</summary>
    public string Detener()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (plugin == null) return Inactivo;
        try { plugin.CallStatic("detener"); }
        catch (System.Exception e) { Debug.LogError($"[GrabacionPantalla] Error al detener: {e.Message}"); }
#endif
        return Estado;
    }

    // Respaldo: si la app se cierra sin pasar por GameController.CerrarAplicacion
    private void OnApplicationQuit()
    {
        if (EstaGrabando) Detener();
    }

    // ── Conversión del reloj de Java (System.nanoTime) al reloj LSL ───────────
    // Las dos lecturas seguidas dan el desfase entre relojes; en Android ambos son el
    // reloj monotónico del sistema, así que el desfase debería ser casi cero.
    private double? ASegundosLsl(long nanos)
    {
        if (nanos == 0 || LSLMarkerSender.Instance == null) return null;
        double lslAhora = LSLMarkerSender.Instance.LocalClock();
        long javaAhora = LlamarLong("ahoraNanos");
        if (javaAhora == 0) return null;
        return lslAhora - (javaAhora - nanos) / 1e9;
    }

    private string LlamarString(string metodo)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try { return plugin != null ? plugin.CallStatic<string>(metodo) : ""; }
        catch (System.Exception) { return ""; }
#else
        return "";
#endif
    }

    private int LlamarInt(string metodo)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try { return plugin != null ? plugin.CallStatic<int>(metodo) : 0; }
        catch (System.Exception) { return 0; }
#else
        return 0;
#endif
    }

    private long LlamarLong(string metodo)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try { return plugin != null ? plugin.CallStatic<long>(metodo) : 0L; }
        catch (System.Exception) { return 0L; }
#else
        return 0L;
#endif
    }
}

using UnityEngine;

/// <summary>
/// Destellos de sincronía para la app TET (ver GAME-SPECS.md §2–§3).
///
/// Cada destello es un cuadrado blanco con marco negro, fijo a la cabeza, unos
/// 10° por encima del centro de la vista, visible 300 ms. En el MISMO cuadro en
/// que se activa se envía el marcador "sync_k" por el stream TET de
/// LSLMarkerSender, con la marca de tiempo LSL de ese instante.
///
///   sync_1   → IniciarPartida() (junto con "inicio partida", misma marca de tiempo)
///   sync_k   → cada 60 s de partida, salvo que el final quede a menos de 10 s
///   sync_N   → FinalizarPartida() (junto con "fin partida"), antes de la pantalla final
///
/// No hace falta ponerlo en la escena: GameController lo crea con Asegurar()
/// y se cuelga de Camera.main (CenterEyeAnchor) al iniciar la partida.
/// </summary>
public class SyncFlash : MonoBehaviour
{
    public static SyncFlash Instance { get; private set; }

    [Header("Temporización (GAME-SPECS §2)")]
    [SerializeField] private float intervaloSegundos = 60f;
    [SerializeField] private float duracionSegundos = 0.3f;
    [Tooltip("Separación mínima entre dos destellos")]
    [SerializeField] private float separacionMinimaSegundos = 10f;

    [Header("Forma (GAME-SPECS §3.2)")]
    [SerializeField] private float distanciaMetros = 1f;
    [Tooltip("Tamaño del cuadrado blanco en grados de ángulo visual")]
    [SerializeField] private float tamanoGrados = 8f;
    [Tooltip("Grosor del marco negro en grados")]
    [SerializeField] private float marcoGrados = 1f;
    [Tooltip("Grados por encima de la línea de la mirada")]
    [SerializeField] private float elevacionGrados = 10f;

    private GameObject destello;
    private float inicioDestelloRealtime;
    private bool destelloVisible;

    private bool partidaActiva;
    private int contadorSync = 0;
    private float ultimoSyncRealtime;
    private float proximoSyncRealtime;

    /// <summary>Cuánto dura visible cada destello, en segundos.</summary>
    public float DuracionSegundos => duracionSegundos;

    /// <summary>Devuelve la instancia, creándola si no existe.</summary>
    public static SyncFlash Asegurar()
    {
        if (Instance == null)
            new GameObject("SyncFlash").AddComponent<SyncFlash>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { enabled = false; Destroy(gameObject); }
    }

    // ===================================================================
    //  API para GameController
    // ===================================================================

    /// <summary>sync_1 + "inicio partida". Llamar en el cuadro de la pulsación.</summary>
    public void IniciarPartida()
    {
        contadorSync = 0;
        partidaActiva = true;
        Destellar("inicio partida");
        proximoSyncRealtime = ultimoSyncRealtime + intervaloSegundos;
    }

    /// <summary>sync_N + "fin partida". Llamar antes de mostrar la pantalla final.</summary>
    public void FinalizarPartida()
    {
        if (!partidaActiva) return;
        partidaActiva = false;

        float separacion = Time.realtimeSinceStartup - ultimoSyncRealtime;
        if (separacion < separacionMinimaSegundos)
            Debug.LogWarning($"[SyncFlash] sync final a sólo {separacion:0.0} s del anterior (mínimo {separacionMinimaSegundos} s).");

        Destellar("fin partida");
    }

    // ===================================================================

    private void Update()
    {
        // Apagado de golpe (sin fundido) al cumplir la duración
        if (destelloVisible && Time.realtimeSinceStartup - inicioDestelloRealtime >= duracionSegundos)
        {
            destello.SetActive(false);
            destelloVisible = false;
        }

        // Destellos intermedios cada 60 s (reloj real: sigue corriendo en pausas)
        if (partidaActiva && Time.realtimeSinceStartup >= proximoSyncRealtime)
        {
            proximoSyncRealtime += intervaloSegundos;

            // Si el final puede llegar en menos de 10 s se omite este destello y
            // queda sólo el del final.
            bool permitido = GameController.Instance == null
                || GameController.Instance.PermiteSyncIntermedio(separacionMinimaSegundos);
            if (permitido) Destellar(null);
            else Debug.Log("[SyncFlash] Destello intermedio omitido: el final está a menos de " +
                           $"{separacionMinimaSegundos} s.");
        }
    }

    /// <summary>
    /// Activa el destello y, en el mismo cuadro, envía sync_k (y el evento
    /// acompañante, si lo hay) con una única marca de tiempo LSL.
    /// </summary>
    private void Destellar(string eventoAcompanante)
    {
        AsegurarDestello();

        destello.SetActive(true);
        destelloVisible = true;
        inicioDestelloRealtime = Time.realtimeSinceStartup;
        ultimoSyncRealtime = inicioDestelloRealtime;

        contadorSync++;
        string etiqueta = $"sync_{contadorSync}";
        double lslTime = LSLMarkerSender.Instance != null ? LSLMarkerSender.Instance.LocalClock() : 0.0;

        LSLMarkerSender.Instance?.SendTet(etiqueta, lslTime);
        if (eventoAcompanante != null) LSLMarkerSender.Instance?.SendTet(eventoAcompanante, lslTime);

        EventLogger.Instance?.SyncFlash(contadorSync, lslTime);
    }

    // ===================================================================
    //  Construcción del cuadrado (cuadrado blanco sobre cuadrado negro)
    // ===================================================================

    private void AsegurarDestello()
    {
        if (destello != null && destello.transform.parent != null) return;

        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("[SyncFlash] No hay Camera.main (CenterEyeAnchor con tag MainCamera).");
            return;
        }
        if (destello != null) Destroy(destello);

        Shader shader = Resources.Load<Shader>("SyncFlashUnlit");
        if (shader == null)
        {
            Debug.LogError("[SyncFlash] No se encontró Resources/SyncFlashUnlit.shader.");
            return;
        }

        // Raíz fija a la cabeza: hija de la cámara, elevada ~10° y mirando al ojo
        destello = new GameObject("SyncFlashQuad");
        destello.transform.SetParent(cam.transform, false);
        float elevRad = elevacionGrados * Mathf.Deg2Rad;
        destello.transform.localPosition = new Vector3(0f, Mathf.Sin(elevRad), Mathf.Cos(elevRad)) * distanciaMetros;
        destello.transform.localRotation = Quaternion.Euler(-elevacionGrados, 0f, 0f);

        // Tamaño lineal que subtiende un ángulo dado a esa distancia
        float Lado(float grados) => 2f * distanciaMetros * Mathf.Tan(grados * 0.5f * Mathf.Deg2Rad);

        CrearCuadrado("Marco", Lado(tamanoGrados + 2f * marcoGrados), Color.black, shader, 4100, short.MaxValue - 1);
        CrearCuadrado("Blanco", Lado(tamanoGrados), Color.white, shader, 4101, short.MaxValue);

        destello.SetActive(false);
    }

    private void CrearCuadrado(string nombre, float lado, Color color, Shader shader, int renderQueue, int ordenDibujo)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = nombre;
        // Sin collider: el rayo del Blaster no debe chocar con el destello
        Destroy(quad.GetComponent<Collider>());
        quad.transform.SetParent(destello.transform, false);
        quad.transform.localScale = new Vector3(lado, lado, 1f);

        var mat = new Material(shader) { renderQueue = renderQueue };
        mat.SetColor("_Color", color);

        var rend = quad.GetComponent<MeshRenderer>();
        rend.sharedMaterial = mat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        // URP ordena lo transparente primero por capa y orden de dibujo, y después por
        // cola: con el orden máximo, el destello se dibuja después de cualquier texto
        // (TextMeshPro) o partícula aunque estos tengan una cola alta.
        rend.sortingOrder = ordenDibujo;
    }
}

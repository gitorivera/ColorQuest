using UnityEngine;
using UnityEngine.InputSystem;

// Disparo de la versión tablet: cada toque nuevo lanza un rayo desde la cámara por el
// punto tocado y lo resuelve igual que el gatillo del Blaster en la Quest
// (ResolutorDisparo: mismos botones, mismo orden de registro, mismos efectos).
//
// Diferencias con la Quest, inherentes a la pantalla táctil:
//  - No hay láser ni hover: el dedo no apunta antes de tocar, así que no hay
//    sphere_hover ni pulso háptico de hover, y los campos de hover van vacíos.
//  - No hay cañón: barrel_pitch_deg = 0 y el rayo sale de la cámara.
//  - trigger_pull agrega screen_position_px (punto tocado en píxeles).
//
// En el Editor sin pantalla táctil se dispara con el clic izquierdo del ratón.
public class TouchShooter : MonoBehaviour
{
    [SerializeField, Tooltip("Vacío = Camera.main")] private Camera camara;
    public LayerMask capaImpacto = 1;   // Default, como el Blaster
    [SerializeField] private ResolutorDisparo.Efectos efectos = new ResolutorDisparo.Efectos
    {
        alcance = 5f,
        tiempoVidaRayo = 0.3f
    };

    [Header("Sonido")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip sonidoDisparo;
    // Igual que el Blaster: 0.708 = -3 dB (compensa el +3 dB del archivo original).
    [SerializeField, Range(0f, 1f)] private float volumenDisparo = 0.708f;

    [Header("Rayo visible")]
    [SerializeField, Tooltip("Dibuja el rayo del disparo desde abajo de la pantalla hasta el impacto (desde la cámara sería sólo un punto)")]
    private bool dibujarRayo = true;
    [SerializeField, Tooltip("Origen del rayo dibujado en coordenadas de viewport (x, y) y distancia a la cámara (z, m)")]
    private Vector3 origenRayoViewport = new Vector3(0.5f, 0f, 0.4f);

    [Header("Registro de puntería")]
    [SerializeField, Range(0f, 10f), Tooltip("Igual que en el Blaster: un disparo que no toca esfera pero pasa a menos de estos grados de una cuenta como casi acierto")]
    private float umbralCasiAciertoGrados = 2f;

    void Awake()
    {
        if (camara == null) camara = Camera.main;
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }

    void Update()
    {
        if (camara == null) return;

        // Cada dedo que toca la pantalla en este cuadro es un disparo, como cada
        // apretón del gatillo en la Quest.
        Touchscreen pantalla = Touchscreen.current;
        if (pantalla != null)
        {
            foreach (var toque in pantalla.touches)
            {
                if (toque.press.wasPressedThisFrame)
                    Disparar(toque.position.ReadValue());
            }
        }
        else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Disparar(Mouse.current.position.ReadValue());
        }
    }

    void Disparar(Vector2 posicionPantalla)
    {
        if (audioSource != null && sonidoDisparo != null)
            audioSource.PlayOneShot(sonidoDisparo, volumenDisparo);

        Ray ray = camara.ScreenPointToRay(posicionPantalla);
        Vector3 origenVisible = camara.ViewportToWorldPoint(origenRayoViewport);

        var efectosDisparo = efectos;
        if (!dibujarRayo) efectosDisparo.rayoPrefab = null;

        ResolutorDisparo.Resolver(ray, capaImpacto, origenVisible, efectosDisparo,
            () => MedirPunteria(ray, posicionPantalla));
    }

    EventLogger.AimInfo MedirPunteria(Ray ray, Vector2 posicionPantalla)
    {
        var m = new EventLogger.AimInfo
        {
            controller = "Touch",
            barrelPitchDeg = 0f,
            hoveredSphereId = "",
            hoverDwellMs = 0,
            hoveredAtPressStartId = "",
            nearMissThresholdDeg = umbralCasiAciertoGrados,
            screenPositionPx = posicionPantalla
        };
        ResolutorDisparo.MedirTablero(ref m, ray.origin, ray.direction);
        return m;
    }
}

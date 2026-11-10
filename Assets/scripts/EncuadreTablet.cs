using UnityEngine;

// Cámara fija de la versión tablet. Se coloca donde estaría el jugador en la Quest
// (a `distancia` del plano del tablero, mirando de frente) y ajusta el campo de visión
// vertical para que toda la zona de juego (tablero 1 x 1 m, barra de tiempo, puntaje,
// instrucciones y botón) quepa en la pantalla con cualquier proporción u orientación.
//
// El ángulo visual real del tablero en la tablet depende del tamaño físico de la
// pantalla y de la distancia a los ojos, no de esta cámara: GameController registra
// display_info (píxeles, dpi, FOV de la cámara) al iniciar la sesión para reconstruirlo.
[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class EncuadreTablet : MonoBehaviour
{
    [SerializeField, Tooltip("Centro de la zona que debe verse completa (m, mundo)")]
    private Vector3 centroZona = new Vector3(0f, 1.175f, 1.5f);
    [SerializeField, Tooltip("Ancho y alto de esa zona (m)")]
    private Vector2 tamanoZona = new Vector2(1.15f, 1.45f);
    [SerializeField, Tooltip("Distancia de la cámara al plano del tablero (m). 1,5 = posición del jugador en la Quest")]
    private float distancia = 1.5f;
    [SerializeField, Range(1f, 1.3f), Tooltip("Margen alrededor de la zona")]
    private float margen = 1.05f;

    private Camera cam;

    void OnEnable() => Ajustar();
    void LateUpdate() => Ajustar();   // la orientación o la resolución pueden cambiar

    void Ajustar()
    {
        if (cam == null) cam = GetComponent<Camera>();
        if (cam == null || distancia <= 0f) return;

        transform.SetPositionAndRotation(centroZona - Vector3.forward * distancia, Quaternion.identity);

        float aspecto = cam.aspect > 0f ? cam.aspect : 1f;
        float medioAlto = Mathf.Max(tamanoZona.y * 0.5f, tamanoZona.x * 0.5f / aspecto) * margen;
        cam.fieldOfView = 2f * Mathf.Atan(medioAlto / distancia) * Mathf.Rad2Deg;
    }
}

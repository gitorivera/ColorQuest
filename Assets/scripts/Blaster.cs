using UnityEngine;

public class Blaster : MonoBehaviour
{
    [SerializeField] private OVRInput.Controller controlador = OVRInput.Controller.RTouch;
    [SerializeField] private LineRenderer rayoPrefab;
    [SerializeField] private Transform puntoDisparo;
    [SerializeField] private float fuerzaDisparo = 5f;
    [SerializeField] private float tiempoVidaRayo = 0.1f;
    public LayerMask capaImpacto;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip sonidoDisparo;
    // Escala lineal del sonido de disparo: 0.708 = -3 dB (compensa el +3 dB del archivo original).
    [SerializeField, Range(0f, 1f)] private float volumenDisparo = 0.708f;
    [SerializeField] private GameObject impactoRayoPrefab;
    [SerializeField] private GameObject ExplosionParticulasPrefab;

    [Header("Gatillo")]
    // Disparar con media presión reduce el movimiento del control al apretar (efecto Heisenberg).
    [SerializeField, Range(0.05f, 1f), Tooltip("Presión del gatillo (0-1) a partir de la cual se dispara")]
    private float umbralDisparo = 0.3f;
    [SerializeField, Range(0f, 1f), Tooltip("Presión por debajo de la cual el gatillo se rearma para el siguiente disparo")]
    private float umbralRearme = 0.15f;

    [Header("Láser de puntería")]
    [SerializeField] private float anchoLaser = 0.002f;
    [SerializeField] private Color colorLaser = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] private Color colorLaserSobreEsfera = new Color(1f, 1f, 1f, 0.85f);
    [SerializeField, Tooltip("Opcional; si se deja vacío se usa una esfera pequeña blanca")]
    private GameObject cursorPrefab;
    [SerializeField] private float diametroCursor = 0.015f;
    [SerializeField, Range(1f, 1.5f), Tooltip("Escala de la esfera mientras el láser está sobre ella")]
    private float factorResaltado = 1.15f;

    private const int CapaIgnorarRaycast = 2;

    private LineRenderer laser;
    private Transform cursor;
    private ResaltadoEsfera esferaResaltada;
    private bool gatilloArmado = true;

    void Awake()
    {
        CrearLaser();
        CrearCursor();
    }

    void OnEnable()
    {
        if (laser != null) laser.enabled = true;
    }

    void OnDisable()
    {
        QuitarResaltado();
        if (laser != null) laser.enabled = false;
        if (cursor != null) cursor.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (cursor != null) Destroy(cursor.gameObject);
    }

    void Update()
    {
        ActualizarPunteria();

        // Disparo por umbral analógico con histéresis: dispara al cruzar umbralDisparo
        // y no vuelve a disparar hasta que la presión baje de umbralRearme.
        float presion = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, controlador);
        if (gatilloArmado && presion >= umbralDisparo)
        {
            gatilloArmado = false;
            Shoot();
        }
        else if (!gatilloArmado && presion <= umbralRearme)
        {
            gatilloArmado = true;
        }
    }

    void CrearLaser()
    {
        laser = new GameObject("LaserPunteria").AddComponent<LineRenderer>();
        laser.transform.SetParent(transform, false);
        laser.useWorldSpace = true;
        laser.positionCount = 2;
        laser.startWidth = anchoLaser;
        laser.endWidth = anchoLaser * 0.5f;
        laser.numCapVertices = 2;
        laser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        laser.receiveShadows = false;
        // Mismo material del rayo de disparo (respeta el color por vértice del LineRenderer)
        if (rayoPrefab != null) laser.sharedMaterial = rayoPrefab.sharedMaterial;
    }

    void CrearCursor()
    {
        GameObject go;
        if (cursorPrefab != null)
        {
            go = Instantiate(cursorPrefab);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.transform.localScale = Vector3.one * diametroCursor;
            Renderer r = go.GetComponent<Renderer>();
            r.material.color = Color.white;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
        go.name = "CursorPunteria";

        // El cursor nunca debe interceptar el raycast de puntería ni el de disparo
        foreach (Collider c in go.GetComponentsInChildren<Collider>()) Destroy(c);
        foreach (Transform t in go.GetComponentsInChildren<Transform>()) t.gameObject.layer = CapaIgnorarRaycast;

        cursor = go.transform;
        cursor.gameObject.SetActive(false);
    }

    // Láser + cursor siempre visibles; resaltado sólo sobre esferas del tablero durante el juego.
    void ActualizarPunteria()
    {
        Vector3 origen = puntoDisparo.position;
        Vector3 direccion = puntoDisparo.forward;
        bool hayImpacto = Physics.Raycast(origen, direccion, out RaycastHit hit, Mathf.Infinity, capaImpacto);

        ResaltadoEsfera nueva = null;
        if (hayImpacto && GameController.Instance != null && GameController.Instance.JuegoActivo
            && GameController.Instance.EsEsferaInstanciada(hit.collider.gameObject))
        {
            GameObject esfera = hit.collider.gameObject;
            nueva = esfera.GetComponent<ResaltadoEsfera>();
            if (nueva == null) nueva = esfera.AddComponent<ResaltadoEsfera>();
        }

        if (nueva != esferaResaltada)
        {
            QuitarResaltado();
            if (nueva != null) nueva.Entrar(factorResaltado);
            esferaResaltada = nueva;
        }

        Vector3 fin = hayImpacto ? hit.point : origen + direccion * fuerzaDisparo;
        laser.SetPosition(0, origen);
        laser.SetPosition(1, fin);
        Color color = nueva != null ? colorLaserSobreEsfera : colorLaser;
        laser.startColor = color;
        laser.endColor = color;

        cursor.gameObject.SetActive(hayImpacto);
        if (hayImpacto) cursor.position = hit.point + hit.normal * 0.002f;
    }

    void QuitarResaltado()
    {
        // Si la esfera ya fue destruida (por un acierto), Unity la compara como null
        if (esferaResaltada != null) esferaResaltada.Salir();
        esferaResaltada = null;
    }

    void Shoot()
    {
        audioSource.PlayOneShot(sonidoDisparo, volumenDisparo);
        Ray ray = new Ray(puntoDisparo.position, puntoDisparo.forward);
        bool hasHit = Physics.Raycast(ray, out RaycastHit hitInfo, Mathf.Infinity, capaImpacto);
        Vector3 puntoFinal = Vector3.zero;
        string hitType = "miss";
        string targetObjectId = "";
        GameObject objetoImpactado = null;

        // 1. DETECT — classify hit without side effects
        if (hasHit)
        {
            puntoFinal = hitInfo.point;
            objetoImpactado = hitInfo.collider.gameObject;
            if (GameController.Instance != null)
            {
                if (GameController.Instance.EsEsferaInstanciada(objetoImpactado))
                {
                    hitType = "sphere";
                    targetObjectId = objetoImpactado.name.Replace("(Clone)", "").Trim();
                }
                else
                {
                    hitType = "grid";
                    targetObjectId = objetoImpactado.name;
                }
            }
        }
        else
        {
            puntoFinal = puntoDisparo.position + puntoDisparo.forward * fuerzaDisparo;
        }

        // Arranque del juego: si estamos en la pantalla de inicio y el disparo golpea
        // el botón, empezamos la sesión con el mismo gesto de disparo que se usa para jugar.
        // (El sonido ya sonó arriba y el rayo se dibuja al final; sólo iniciamos aquí.)
        if (hasHit && GameController.Instance != null
            && GameController.Instance.EsperandoInicio
            && GameController.Instance.EsBotonInicio(objetoImpactado))
        {
            GameController.Instance.IniciarJuego();
        }

        // El juego sólo reacciona a los disparos mientras se está jugando.
        bool juegoActivo = GameController.Instance != null && GameController.Instance.JuegoActivo;

        // 2. LOG trigger_pull before any hit handling so it precedes sphere_hit_correct
        //    (sólo se registra durante el juego, no en menús ni presentaciones)
        if (juegoActivo && EventLogger.Instance != null)
        {
            EventLogger.Instance.TriggerPull(hasHit, hitType, targetObjectId,
                puntoDisparo.position, puntoDisparo.forward,
                GameController.Instance.GetRTSinceBoardSpawn());
        }

        // 3. ACT — call EsferaClickeada / spawn effects (sólo si el juego está activo)
        if (hasHit && juegoActivo)
        {
            if (hitType == "sphere")
            {
                GameController.Instance.EsferaClickeada(objetoImpactado);
                GameObject explosion = Instantiate(ExplosionParticulasPrefab, puntoFinal, Quaternion.identity);
                Renderer rendererEsfera = objetoImpactado.GetComponent<Renderer>();
                if (rendererEsfera != null)
                {
                    Color colorEsfera = rendererEsfera.material.color;
                    var particleSystem = explosion.GetComponent<ParticleSystem>();
                    var mainModule = particleSystem.main;
                    mainModule.startColor = colorEsfera;
                }
                Destroy(objetoImpactado);
                Destroy(explosion, 2f);
            }
            else
            {
                Quaternion rotacionImpacto = Quaternion.LookRotation(-hitInfo.normal);
                GameObject impacto = Instantiate(impactoRayoPrefab, puntoFinal, rotacionImpacto);
                Destroy(impacto, 1f);
            }
        }

        // 4. Draw the ray
        if (GameController.Instance != null)
        {
            LineRenderer rayo = Instantiate(rayoPrefab);
            rayo.positionCount = 2;
            rayo.SetPosition(0, puntoDisparo.position);
            rayo.SetPosition(1, puntoFinal);
            Destroy(rayo.gameObject, tiempoVidaRayo);
        }
    }
}

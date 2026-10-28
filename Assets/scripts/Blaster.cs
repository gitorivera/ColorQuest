using System.Collections;
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

    [Header("Háptico de hover")]
    [SerializeField, Tooltip("Pulso corto en el control al entrar el láser sobre una esfera")]
    private bool hapticoHover = true;
    [SerializeField, Range(0f, 1f)] private float amplitudHapticoHover = 0.3f;
    [SerializeField, Range(0f, 1f)] private float frecuenciaHapticoHover = 0.5f;
    [SerializeField, Range(0.01f, 0.2f), Tooltip("Duración del pulso en segundos")]
    private float duracionHapticoHover = 0.04f;
    [SerializeField, Range(0f, 0.5f), Tooltip("Segundos tras salir de una esfera en los que volver a entrar en ella no repite el pulso ni el evento (el láser rozando el borde)")]
    private float refractarioHover = 0.15f;

    [Header("Orientación del cañón")]
    // El rayo sale por el +Z del HandAnchor del control. Este ángulo inclina el blaster
    // completo (modelo y puntoDisparo) alrededor del eje X del control, como el ajuste
    // de ángulo del arma de otros juegos VR. Debe ser el mismo para todos los participantes.
    [SerializeField, Range(-60f, 60f), Tooltip("Inclinación del cañón en grados. Positivo = cañón más abajo")]
    private float anguloCanon = 0f;
    [SerializeField, Tooltip("Sólo para calibrar en pruebas: el stick arriba/abajo cambia el ángulo en pasos. Desactivar con participantes")]
    private bool ajusteConStick = false;
    [SerializeField, Range(1f, 15f)] private float pasoAjusteCanon = 5f;

    [Header("Registro de puntería")]
    [SerializeField, Range(0f, 10f), Tooltip("Un disparo que no toca esfera pero pasa a menos de estos grados de una cuenta como casi acierto")]
    private float umbralCasiAciertoGrados = 2f;

    private const int CapaIgnorarRaycast = 2;
    private const float UmbralInicioApreton = 0.05f;

    private LineRenderer laser;
    private Transform cursor;
    private ResaltadoEsfera esferaResaltada;
    private float inicioHover;
    private ResaltadoEsfera ultimaEsferaSalida;
    private float tiempoSalida;
    private bool gatilloArmado = true;
    private bool apretonIniciado;
    private string esferaAlApretar = "";
    private Coroutine pulsoHover;
    private Quaternion rotacionBase;
    private Vector3 posicionBase;
    private float anguloAplicado = float.NaN;
    private bool stickArmado = true;

    void Awake()
    {
        rotacionBase = transform.localRotation;
        posicionBase = transform.localPosition;
        AplicarAnguloCanon();
        CrearLaser();
        CrearCursor();
    }

    void OnEnable()
    {
        if (laser != null) laser.enabled = true;
    }

    void OnDisable()
    {
        DetenerPulsoHover();
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
        if (ajusteConStick) AjustarAnguloConStick();
        AplicarAnguloCanon();
        ActualizarPunteria();

        // Disparo por umbral analógico con histéresis: dispara al cruzar umbralDisparo
        // y no vuelve a disparar hasta que la presión baje de umbralRearme.
        float presion = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, controlador);
        // Esfera apuntada cuando el dedo empieza a apretar: si difiere de la del disparo,
        // el propio apretón movió la puntería.
        if (gatilloArmado && !apretonIniciado && presion >= UmbralInicioApreton)
        {
            apretonIniciado = true;
            esferaAlApretar = esferaResaltada != null ? IdEsfera(esferaResaltada.gameObject) : "";
        }
        else if (gatilloArmado && presion < UmbralInicioApreton)
        {
            apretonIniciado = false;
        }

        if (gatilloArmado && presion >= umbralDisparo)
        {
            gatilloArmado = false;
            Shoot();
        }
        else if (!gatilloArmado && presion <= umbralRearme)
        {
            gatilloArmado = true;
            apretonIniciado = false;
        }
    }

    // Gira el blaster alrededor del origen del HandAnchor (el agarre), así el rayo
    // sigue saliendo por la boca del cañón. Se reaplica sólo si el ángulo cambió
    // (stick o Inspector en Play).
    void AplicarAnguloCanon()
    {
        if (anguloCanon == anguloAplicado) return;
        Quaternion inclinacion = Quaternion.AngleAxis(anguloCanon, Vector3.right);
        transform.localRotation = inclinacion * rotacionBase;
        transform.localPosition = inclinacion * posicionBase;
        anguloAplicado = anguloCanon;
    }

    // Un toque del stick = un paso; hay que soltarlo para dar el siguiente.
    void AjustarAnguloConStick()
    {
        float y = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, controlador).y;
        if (stickArmado && Mathf.Abs(y) > 0.7f)
        {
            stickArmado = false;
            // Stick arriba = cañón más arriba = ángulo negativo
            anguloCanon = Mathf.Clamp(anguloCanon - Mathf.Sign(y) * pasoAjusteCanon, -60f, 60f);
            Debug.Log($"[Blaster] {controlador} anguloCanon = {anguloCanon}°");
        }
        else if (!stickArmado && Mathf.Abs(y) < 0.3f)
        {
            stickArmado = true;
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
        if (hayImpacto && GameController.Instance != null && GameController.Instance.PunteriaActiva
            && GameController.Instance.EsEsferaInstanciada(hit.collider.gameObject))
        {
            GameObject esfera = hit.collider.gameObject;
            nueva = esfera.GetComponent<ResaltadoEsfera>();
            if (nueva == null) nueva = esfera.AddComponent<ResaltadoEsfera>();
        }

        if (nueva != esferaResaltada)
        {
            if (esferaResaltada != null)
            {
                ultimaEsferaSalida = esferaResaltada;
                tiempoSalida = Time.realtimeSinceStartup;
            }
            QuitarResaltado();
            if (nueva != null)
            {
                // El resaltado sigue al rayo exactamente, para no engañar sobre dónde pegará el disparo
                nueva.Entrar(factorResaltado);
                // Reentrada rápida en la misma esfera (el láser roza el borde): es el mismo hover,
                // sin nuevo pulso ni evento, y el tiempo de permanencia sigue contando desde la entrada.
                bool reentrada = nueva == ultimaEsferaSalida
                                 && Time.realtimeSinceStartup - tiempoSalida < refractarioHover;
                if (!reentrada)
                {
                    if (hapticoHover) IniciarPulsoHover();
                    inicioHover = Time.realtimeSinceStartup;
                    // Cada hover (y su pulso háptico) se registra: es un estímulo táctil
                    if (GameController.Instance.JuegoActivo && EventLogger.Instance != null)
                    {
                        EventLogger.Instance.SphereHover(IdEsfera(nueva.gameObject), controlador.ToString(),
                            hapticoHover, amplitudHapticoHover, frecuenciaHapticoHover,
                            Mathf.RoundToInt(duracionHapticoHover * 1000f),
                            Mathf.RoundToInt(refractarioHover * 1000f));
                    }
                }
            }
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

    void IniciarPulsoHover()
    {
        DetenerPulsoHover();
        pulsoHover = StartCoroutine(PulsoHover());
    }

    IEnumerator PulsoHover()
    {
        OVRInput.SetControllerVibration(frecuenciaHapticoHover, amplitudHapticoHover, controlador);
        yield return new WaitForSeconds(duracionHapticoHover);
        OVRInput.SetControllerVibration(0f, 0f, controlador);
        pulsoHover = null;
    }

    // Corta un pulso en curso. Se llama al disparar para que, si el disparo provoca
    // la vibración de error de GameController, el fin del pulso no la apague.
    void DetenerPulsoHover()
    {
        if (pulsoHover == null) return;
        StopCoroutine(pulsoHover);
        pulsoHover = null;
        OVRInput.SetControllerVibration(0f, 0f, controlador);
    }

    void QuitarResaltado()
    {
        // Si la esfera ya fue destruida (por un acierto), Unity la compara como null
        if (esferaResaltada != null) esferaResaltada.Salir();
        esferaResaltada = null;
    }

    static string IdEsfera(GameObject esfera) => esfera.name.Replace("(Clone)", "").Trim();

    // Error angular del disparo respecto a las esferas del tablero, para separar errores
    // motores (casi aciertos) de errores de secuencia (comisión).
    EventLogger.AimInfo MedirPunteria(Vector3 origen, Vector3 direccion)
    {
        var m = new EventLogger.AimInfo
        {
            controller = controlador.ToString(),
            barrelPitchDeg = anguloCanon,
            hoveredSphereId = esferaResaltada != null ? IdEsfera(esferaResaltada.gameObject) : "",
            hoverDwellMs = esferaResaltada != null ? (long)((Time.realtimeSinceStartup - inicioHover) * 1000f) : 0,
            hoveredAtPressStartId = apretonIniciado ? esferaAlApretar : "",
            nearestSphereId = "",
            expectedSphereId = "",
            nearMissThresholdDeg = umbralCasiAciertoGrados
        };

        foreach (GameObject esfera in GameController.Instance.EsferasEnTablero())
        {
            float borde = AnguloAlBorde(origen, direccion, esfera, out float centro);
            if (!m.nearestEdgeAngleDeg.HasValue || borde < m.nearestEdgeAngleDeg.Value)
            {
                m.nearestSphereId = IdEsfera(esfera);
                m.nearestCenterAngleDeg = centro;
                m.nearestEdgeAngleDeg = borde;
            }
        }

        GameObject esperada = GameController.Instance.EsferaEsperada();
        if (esperada != null)
        {
            m.expectedSphereId = IdEsfera(esperada);
            m.expectedEdgeAngleDeg = AnguloAlBorde(origen, direccion, esperada, out _);
        }
        return m;
    }

    // Ángulo entre el rayo y la silueta de la esfera (≤ 0: el rayo la atraviesa).
    // Usa el collider real, que incluye el agrandamiento del resaltado.
    static float AnguloAlBorde(Vector3 origen, Vector3 direccion, GameObject esfera, out float anguloCentro)
    {
        Collider col = esfera.GetComponent<Collider>();
        Vector3 centro = col != null ? col.bounds.center : esfera.transform.position;
        float radio = col != null ? col.bounds.extents.x : 0f;
        Vector3 hacia = centro - origen;
        float distancia = hacia.magnitude;
        anguloCentro = Vector3.Angle(direccion, hacia);
        float radioAngular = distancia > radio ? Mathf.Asin(radio / distancia) * Mathf.Rad2Deg : 90f;
        return anguloCentro - radioAngular;
    }

    void Shoot()
    {
        DetenerPulsoHover();
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

        // El juego sólo reacciona a los disparos mientras se está jugando o practicando.
        // Se evalúa ANTES de los botones para que el disparo que arranca la práctica
        // no cuente como disparo de práctica.
        bool juegoActivo = GameController.Instance != null && GameController.Instance.JuegoActivo;
        bool punteriaActiva = GameController.Instance != null && GameController.Instance.PunteriaActiva;
        bool enPractica = GameController.Instance != null && GameController.Instance.EnPractica;

        // Arranque de la práctica / del juego: si estamos en la pantalla del botón y el
        // disparo lo golpea, arrancamos con el mismo gesto de disparo que se usa para jugar.
        // (El sonido ya sonó arriba y el rayo se dibuja al final; sólo iniciamos aquí.)
        if (hasHit && GameController.Instance != null)
        {
            if (GameController.Instance.EsperandoPractica
                && GameController.Instance.EsBotonPractica(objetoImpactado))
            {
                GameController.Instance.IniciarPractica();
            }
            else if (GameController.Instance.EsperandoInicio
                && GameController.Instance.EsBotonInicio(objetoImpactado))
            {
                GameController.Instance.IniciarJuego();
            }
        }

        // Los disparos de práctica sólo se cuentan (precisión de línea base), no se registran uno a uno
        if (enPractica) GameController.Instance.RegistrarDisparoPractica();

        // 2. LOG trigger_pull before any hit handling so it precedes sphere_hit_correct
        //    (sólo se registra durante el juego, no en menús ni presentaciones)
        if (juegoActivo && EventLogger.Instance != null)
        {
            EventLogger.Instance.TriggerPull(hasHit, hitType, targetObjectId,
                puntoDisparo.position, puntoDisparo.forward,
                GameController.Instance.GetRTSinceBoardSpawn(),
                MedirPunteria(puntoDisparo.position, puntoDisparo.forward));
        }

        // 3. ACT — call EsferaClickeada / spawn effects (sólo jugando o practicando)
        if (hasHit && punteriaActiva)
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

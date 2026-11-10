using System;
using UnityEngine;

// Resolución de un disparo, común al Blaster (gatillo del control en la Quest) y al
// TouchShooter (toque en la tablet). Clasifica el impacto, arranca la práctica o el
// juego si el rayo golpea su botón, registra trigger_pull, avisa a GameController y
// crea los efectos. Tenerla en un solo sitio garantiza que las dos versiones del juego
// reaccionen y registren lo mismo, en el mismo orden, para poder compararlas.
public static class ResolutorDisparo
{
    [Serializable]
    public struct Efectos
    {
        public LineRenderer rayoPrefab;
        public GameObject impactoRayoPrefab;
        public GameObject explosionParticulasPrefab;
        [Tooltip("Largo del rayo dibujado cuando no golpea nada (m)")]
        public float alcance;
        [Tooltip("Segundos que se ve el rayo del disparo")]
        public float tiempoVidaRayo;
    }

    // ray: el rayo que decide el impacto (cañón del control o cámara → punto tocado).
    // origenRayoVisible: desde dónde se dibuja el rayo (en la Quest coincide con ray.origin).
    // medirPunteria: sólo se llama si el disparo se registra (durante el juego).
    public static void Resolver(Ray ray, LayerMask capaImpacto, Vector3 origenRayoVisible,
                                Efectos efectos, Func<EventLogger.AimInfo> medirPunteria)
    {
        bool hasHit = Physics.Raycast(ray, out RaycastHit hitInfo, Mathf.Infinity, capaImpacto);
        Vector3 puntoFinal;
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
                    targetObjectId = IdEsfera(objetoImpactado);
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
            puntoFinal = ray.origin + ray.direction * efectos.alcance;
        }

        // El juego sólo reacciona a los disparos mientras se está jugando o practicando.
        // Se evalúa ANTES de los botones para que el disparo que arranca la práctica
        // no cuente como disparo de práctica.
        bool juegoActivo = GameController.Instance != null && GameController.Instance.JuegoActivo;
        bool punteriaActiva = GameController.Instance != null && GameController.Instance.PunteriaActiva;
        bool enPractica = GameController.Instance != null && GameController.Instance.EnPractica;

        // Arranque de la práctica / del juego: si estamos en la pantalla del botón y el
        // disparo lo golpea, arrancamos con el mismo gesto de disparo que se usa para jugar.
        // (El sonido ya sonó en quien dispara y el rayo se dibuja al final; sólo iniciamos aquí.)
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
                ray.origin, ray.direction,
                GameController.Instance.GetRTSinceBoardSpawn(),
                medirPunteria());
        }

        // 3. ACT — call EsferaClickeada / spawn effects (sólo jugando o practicando)
        if (hasHit && punteriaActiva)
        {
            if (hitType == "sphere")
            {
                GameController.Instance.EsferaClickeada(objetoImpactado);
                GameObject explosion = UnityEngine.Object.Instantiate(efectos.explosionParticulasPrefab, puntoFinal, Quaternion.identity);
                Renderer rendererEsfera = objetoImpactado.GetComponent<Renderer>();
                if (rendererEsfera != null)
                {
                    Color colorEsfera = rendererEsfera.material.color;
                    var particleSystem = explosion.GetComponent<ParticleSystem>();
                    var mainModule = particleSystem.main;
                    mainModule.startColor = colorEsfera;
                }
                UnityEngine.Object.Destroy(objetoImpactado);
                UnityEngine.Object.Destroy(explosion, 2f);
            }
            else
            {
                Quaternion rotacionImpacto = Quaternion.LookRotation(-hitInfo.normal);
                GameObject impacto = UnityEngine.Object.Instantiate(efectos.impactoRayoPrefab, puntoFinal, rotacionImpacto);
                UnityEngine.Object.Destroy(impacto, 1f);
            }
        }

        // 4. Draw the ray
        if (GameController.Instance != null && efectos.rayoPrefab != null)
        {
            LineRenderer rayo = UnityEngine.Object.Instantiate(efectos.rayoPrefab);
            rayo.positionCount = 2;
            rayo.SetPosition(0, origenRayoVisible);
            rayo.SetPosition(1, puntoFinal);
            UnityEngine.Object.Destroy(rayo.gameObject, efectos.tiempoVidaRayo);
        }
    }

    // Completa el error angular del disparo respecto a las esferas del tablero (más
    // cercana y esperada), para separar errores motores (casi aciertos) de errores de
    // secuencia (comisión).
    public static void MedirTablero(ref EventLogger.AimInfo m, Vector3 origen, Vector3 direccion)
    {
        m.nearestSphereId = "";
        m.expectedSphereId = "";
        if (GameController.Instance == null) return;

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
    }

    public static string IdEsfera(GameObject esfera) => esfera.name.Replace("(Clone)", "").Trim();

    // Ángulo entre el rayo y la silueta de la esfera (≤ 0: el rayo la atraviesa).
    // Usa el collider real, que incluye el agrandamiento del resaltado.
    public static float AnguloAlBorde(Vector3 origen, Vector3 direccion, GameObject esfera, out float anguloCentro)
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
}

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
    [SerializeField] private GameObject impactoRayoPrefab;
    [SerializeField] private GameObject ExplosionParticulasPrefab;

    void Update()
    {
        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, controlador))
        {
            Shoot();
        }
    }

    void Shoot()
    {
        audioSource.PlayOneShot(sonidoDisparo);
        Ray ray = new Ray(puntoDisparo.position, puntoDisparo.forward);
        bool hasHit = Physics.Raycast(ray, out RaycastHit hitInfo, Mathf.Infinity, capaImpacto);
        Vector3 puntoFinal = Vector3.zero;

        if (hasHit)
        {
            puntoFinal = hitInfo.point;
            GameObject objetoImpactado = hitInfo.collider.gameObject;
            if (GameController.Instance != null)
            {
                if (GameController.Instance.EsEsferaInstanciada(objetoImpactado))
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
        }
        else
        {
            puntoFinal = puntoDisparo.position + puntoDisparo.forward * fuerzaDisparo;
        }

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

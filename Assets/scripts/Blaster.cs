using UnityEngine;

public class Blaster : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [SerializeField]
    private OVRInput.RawButton botonDisparo;
    [SerializeField]
    private LineRenderer rayoPrefab;
    [SerializeField]
    private Transform puntoDisparo;
    [SerializeField]
    private float fuerzaDisparo = 5f;
    [SerializeField]
    private float tiempoVidaRayo = 0.1f;
    public LayerMask capaImpacto;
    [SerializeField]
    private AudioSource audioSource;
    [SerializeField]
    private AudioClip sonidoDisparo;
    [SerializeField]
    private GameObject impactoRayoPrefab;
    [SerializeField]
    private GameObject ExplosionParticulasPrefab;


    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (OVRInput.GetDown(botonDisparo))
        {
            Shoot();
        }
    }
    void Shoot()
    {
        // sonido del blaster
        audioSource.PlayOneShot(sonidoDisparo);
        // rayo para detectar colisiones
        Ray ray = new Ray(puntoDisparo.position, puntoDisparo.forward);
        // Detección de las colisiones
        bool hasHit = Physics.Raycast(ray, out RaycastHit hitInfo, Mathf.Infinity, capaImpacto);
        // por defecto el laser no tiene un punto final
        Vector3 puntoFinal=Vector3.zero;

        if (hasHit)
        {
            //detener rayo
            puntoFinal = hitInfo.point;
            // gameobject para identificar la colisión
            GameObject objetoImpactado = hitInfo.collider.gameObject;
            if (GameController.Instance != null)
            {
                if (GameController.Instance.EsEsferaInstanciada(objetoImpactado))
                {
                    GameController.Instance.EsferaClickeada(objetoImpactado);
                    //efecto especial para las esferas
                    GameObject explosion = Instantiate(ExplosionParticulasPrefab, puntoFinal, Quaternion.identity);
                    //Renderer para obtener el color de la esfera
                    Renderer rendererEsfera = objetoImpactado.GetComponent<Renderer>();
                    if (rendererEsfera != null)
                    {
                        Color colorEsfera = rendererEsfera.material.color;
                        // Cambiar el color de las partículas al color de la esfera
                        var particleSystem = explosion.GetComponent<ParticleSystem>();
                        var mainModule = particleSystem.main;
                        mainModule.startColor = colorEsfera;
                    }
                    // Destruir la esfera
                    Destroy(objetoImpactado);
                    //Destruir la explosión después de 2 segundos
                    Destroy(explosion, 2f);
                }
                else
                {
                    //genero efecto de impacto en los colliders de la sala
                    Quaternion rotacionImpacto = Quaternion.LookRotation(-hitInfo.normal);
                    GameObject impacto = Instantiate(impactoRayoPrefab, puntoFinal, rotacionImpacto);
                    Destroy(impacto, 1f);
                }
            }
        }
        else
        {
            // Crear rayo
            puntoFinal = puntoDisparo.position + puntoDisparo.forward * fuerzaDisparo;
        }

        if (GameController.Instance != null)
        {
            LineRenderer rayo = Instantiate(rayoPrefab);
            rayo.positionCount = 2;
            rayo.SetPosition(0, puntoDisparo.position);
            rayo.SetPosition(1,puntoFinal); 
            Destroy(rayo.gameObject, tiempoVidaRayo);
        }
    }
}

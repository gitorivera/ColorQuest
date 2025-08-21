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
        Ray ray = new Ray(puntoDisparo.position, puntoDisparo.forward);
        bool hasHit = Physics.Raycast(ray, out RaycastHit hitInfo, Mathf.Infinity, capaImpacto);

        Vector3 puntoFinal=Vector3.zero;

        if (hasHit)
        {
            //detener rayo
            puntoFinal = hitInfo.point;
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

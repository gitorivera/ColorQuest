using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

public class GameController : MonoBehaviour
{

    [SerializeField]
    [Range(2, 5)]
    
    private int NumeroCeldas = 2;
    [SerializeField]
    private GameObject CeldaPrefab;
    private readonly int anchoMalla = 1;
    private readonly float anchoCeldaPrefab=0.2f;
    private readonly float puntoCentralX = 0f;
    private readonly float puntoCentralY = 1f;
    private readonly float puntoCentralZ = 0f;
    private float posicionX;
    private float posicionY;
    private float posicionZ;
    private GameObject InstanciaDeCelda;

    void CrearMalla()
    {
        //Calculo del ancho de la celda para las instancias creadas
        float anchoCelda = (float)anchoMalla / NumeroCeldas;
        float factorEscala = (float)anchoCelda / anchoCeldaPrefab;
        //Posicionamiento de la celda superior izquierda
        int numSaltosIniciales = NumeroCeldas - 1;
        float longitudSalto = (float)anchoMalla / (NumeroCeldas * 2);
        posicionX = puntoCentralX - (numSaltosIniciales * longitudSalto);
        posicionY = puntoCentralY + (numSaltosIniciales * longitudSalto);
        posicionZ = puntoCentralZ;
        //InstanciaDeCelda = Instantiate(CeldaPrefab, new Vector3(posicionX, posicionY, posicionZ), quaternion.identity);
        InstanciaDeCelda = Instantiate(CeldaPrefab, new Vector3(0, 1, 0), quaternion.identity);
        
    }

    
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }
}


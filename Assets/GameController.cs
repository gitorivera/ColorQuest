using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

public class GameController : MonoBehaviour
{

    [SerializeField]
    [Range(2, 5)]
    
    private int numeroCeldas = 2;
    [SerializeField]
    private GameObject CeldaPrefab;
    private readonly int anchoMalla = 1;
    private readonly float anchoCeldaPrefab=0.2f;
    private readonly float puntoCentralX = 0f;
    private readonly float puntoCentralY = 1f;
    private readonly float puntoCentralZ = 0f;
    private float posicionInicialX;
    private float posicionInicialY;
    private float posicionInicialZ;
    private GameObject InstanciaDeCelda;

    void CrearMalla()
    {
        //Calculo del ancho de la celda para las instancias creadas
        float anchoCelda = (float)anchoMalla / numeroCeldas;
        float factorEscala = (float)anchoCelda / anchoCeldaPrefab;
        //Posicionamiento de la celda superior izquierda
        int numSaltosIniciales = numeroCeldas - 1;
        float longitudSalto = (float)anchoMalla / (numeroCeldas * 2);
        posicionInicialX = puntoCentralX - (numSaltosIniciales * longitudSalto);
        posicionInicialY = puntoCentralY + (numSaltosIniciales * longitudSalto);
        posicionInicialZ = 0;
        InstanciaDeCelda = Instantiate(CeldaPrefab, new Vector3(posicionInicialX, posicionInicialY, 0), quaternion.identity);
        //------------------------------------
        // Posicionamiento de todas las celdas
        //------------------------------------
        //incremento en las posiciones para ubicar las siguientes celdas
        float posicionActualX = posicionInicialX;
        float posicionActualY = posicionInicialY;
        float posicionActualZ = posicionInicialZ;

        float deltaPosicion = longitudSalto*2;
        while (true)
        {
            posicionActualX += deltaPosicion;
            if (posicionActualX < 0.5)
            {
                Instantiate(CeldaPrefab, new Vector3(posicionActualX, posicionActualY, posicionActualZ), quaternion.identity);
            }
            else
            {
                posicionActualX = posicionInicialX;
                posicionActualY -= deltaPosicion;
                if (posicionActualY > 0.5)
                {
                    Instantiate(CeldaPrefab, new Vector3(posicionActualX, posicionActualY, posicionActualZ), quaternion.identity);
                }
                else
                {
                    break;
                }
            }
            
        }
        
    }

    
        void Start()
        {
        CrearMalla();
        }

        // Update is called once per frame
        void Update()
        {

        }
}


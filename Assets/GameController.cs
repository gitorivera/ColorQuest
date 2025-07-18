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
    private readonly float anchoMalla = 1f;
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
        Debug.Log($"Acncho de la celda:{anchoCelda}");
        float factorEscala = (float)anchoCelda / anchoCeldaPrefab;
        Debug.Log($"Factor de escala:{factorEscala}");
        //Posicionamiento de la celda superior izquierda
        int numSaltosIniciales = numeroCeldas - 1;
        Debug.Log($"Numero de saltos iniciales:{numSaltosIniciales}");
        float longitudSalto = (float)anchoMalla / (numeroCeldas * 2);
        Debug.Log($"Longitud del salto:{longitudSalto}");
        posicionInicialX = puntoCentralX - (numSaltosIniciales * longitudSalto);
        Debug.Log($"Posicion inicial X:{posicionInicialX}");
        posicionInicialY = puntoCentralY + (numSaltosIniciales * longitudSalto);
        Debug.Log($"Posicion inicial Y:{posicionInicialY}");
        posicionInicialZ = puntoCentralZ;
        InstanciaDeCelda = Instantiate(CeldaPrefab, new Vector3(posicionInicialX, posicionInicialY, 0), quaternion.identity);
        InstanciaDeCelda.transform.localScale = new Vector3(factorEscala, factorEscala, factorEscala);
        //------------------------------------
        // Posicionamiento de todas las celdas
        //------------------------------------
        //incremento en las posiciones para ubicar las siguientes celdas
        float posicionActualX = posicionInicialX;
        float posicionActualY = posicionInicialY;
        float posicionActualZ = posicionInicialZ;

        //float deltaPosicion = longitudSalto*2;
        float deltaPosicion = 0.95f*anchoCeldaPrefab* factorEscala;
        Debug.Log($"Delta de posicion:{deltaPosicion}");
        while (true)
        {
            posicionActualX += deltaPosicion;
            Debug.Log($"Posicion actual X:{posicionActualX}");
            Debug.Log($"Posicion actual Y:{posicionActualY}");
            Debug.Log($"Posicion actual Z:{posicionActualZ}");

            if (posicionActualX < anchoMalla / 2)
            {
                InstanciaDeCelda = Instantiate(CeldaPrefab, new Vector3(posicionActualX, posicionActualY, posicionActualZ), quaternion.identity);
                InstanciaDeCelda.transform.localScale = new Vector3(factorEscala, factorEscala, factorEscala);
            }
            else
            {
                posicionActualX = posicionInicialX;
                posicionActualY -= deltaPosicion;
                if (posicionActualY > 1-(anchoMalla / 2))
                {
                    InstanciaDeCelda=Instantiate(CeldaPrefab, new Vector3(posicionActualX, posicionActualY, posicionActualZ), quaternion.identity);
                    InstanciaDeCelda.transform.localScale = new Vector3(factorEscala, factorEscala, factorEscala);
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


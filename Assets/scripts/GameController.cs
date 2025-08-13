using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;
//add the random library
using Random = UnityEngine.Random;
using System.Linq;
using TMPro;

public class GameController : MonoBehaviour
{

    [SerializeField][Tooltip("Número de celdas por lado (N x N)")][Range(2, 5)] private int numeroCeldas = 2;
    [SerializeField] private GameObject CeldaPrefab;
    [SerializeField] private float anchoMalla = 1f;
    private readonly float anchoCeldaPrefab = 0.2f;
    private readonly float puntoCentralX = 0f;
    private readonly float puntoCentralY = 1f;
    private readonly float puntoCentralZ = 0f;
    private float posicionInicialX;
    private float posicionInicialY;
    private float posicionInicialZ;
    private GameObject InstanciaDeCelda;
    private List<Vector3> listaPosicionesCeldas = new List<Vector3>();
    private List<Vector3> listaPosicionesBolas = new List<Vector3>();

    [Header("Configuración de las Esferas")]
    [SerializeField] private GameObject[] bolasPrefabs; // Array de prefabs de bola

    private List<GameObject> instanciaBolas = new List<GameObject>();
    [SerializeField]
    [Tooltip("Segundos que duran las esferas en el tablero")]
    [Range(1, 10)]
    private int duracionTablero = 5;

    [SerializeField] TextMeshPro RelojContador;
    private float tiempoActual = 0;
    private int segundosTranscurridos = 0;

    public static GameController instance;
    private int indiceActual = 0;

    [SerializeField] TextMeshPro ContadorPuntos;
    
    private int puntaje = 0;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

    }

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
        posicionInicialZ = puntoCentralZ;

        InstanciaDeCelda = Instantiate(CeldaPrefab, new Vector3(posicionInicialX, posicionInicialY, 0), quaternion.identity);
        InstanciaDeCelda.transform.localScale = new Vector3(factorEscala, factorEscala, factorEscala);
        listaPosicionesCeldas.Add(new Vector3(posicionInicialX, posicionInicialY, posicionInicialZ));
        //------------------------------------
        // Posicionamiento de todas las celdas
        //------------------------------------
        //incremento en las posiciones para ubicar las siguientes celdas
        float posicionActualX = posicionInicialX;
        float posicionActualY = posicionInicialY;
        float posicionActualZ = posicionInicialZ;

        //float deltaPosicion = longitudSalto*2;
        float deltaPosicion = 0.95f * anchoCeldaPrefab * factorEscala;

        while (true)
        {
            posicionActualX += deltaPosicion;


            if (posicionActualX < anchoMalla / 2)
            {
                InstanciaDeCelda = Instantiate(CeldaPrefab, new Vector3(posicionActualX, posicionActualY, posicionActualZ), quaternion.identity);
                InstanciaDeCelda.transform.localScale = new Vector3(factorEscala, factorEscala, factorEscala);
                listaPosicionesCeldas.Add(new Vector3(posicionActualX, posicionActualY, posicionActualZ));
            }
            else
            {
                posicionActualX = posicionInicialX;
                posicionActualY -= deltaPosicion;
                if (posicionActualY > 1 - (anchoMalla / 2))
                {
                    InstanciaDeCelda = Instantiate(CeldaPrefab, new Vector3(posicionActualX, posicionActualY, posicionActualZ), quaternion.identity);
                    InstanciaDeCelda.transform.localScale = new Vector3(factorEscala, factorEscala, factorEscala);
                    listaPosicionesCeldas.Add(new Vector3(posicionActualX, posicionActualY, posicionActualZ));
                }
                else
                {
                    break;
                }
            }

        }

    }

    void PosicionarBolas()
    {
        List<Vector3> posicionesEscogidas = listaPosicionesCeldas
                .OrderBy(x => Random.value)
                .Take(bolasPrefabs.Length)
                .ToList();

        if (instanciaBolas.Count > 0)
        {
            for (int i = 0; i < instanciaBolas.Count; i++)
            {
                instanciaBolas[i].transform.position = posicionesEscogidas[i];
            }
        }
        else
        {
            for (int i = 0; i < bolasPrefabs.Length; i++)
            {
                GameObject instanciaBola = Instantiate(bolasPrefabs[i], posicionesEscogidas[i], quaternion.identity);
                instanciaBolas.Add(instanciaBola);
                instanciaBola.AddComponent<ClickEsfera>();

            }
        }
        //     for (int i = 0; i < 4; i++)
        //    {
        //         GameObject InstanciaBola = Instantiate(bolasPrefabs[i], posicionesEscogidas[i], quaternion.identity);
        //     }
    }

    public void EsferaClickeada(GameObject esfera)
    {

        string esferaNombre = esfera.name.Replace("(Clone)", "").Trim();

        // Verificar si el clic coincide con el orden correcto
        if (bolasPrefabs[indiceActual].name == esferaNombre)
        {
            Debug.Log($"¡Correcto! Bola {esferaNombre} clickeada en el orden correcto.");
            indiceActual++;
            

            // Verificar si se completó el orden, exceptuando la ultima esfera
            if (indiceActual >= bolasPrefabs.Length - 1)
            {
                Debug.Log("¡Felicidades! Has clickeado todas las bolas en el orden correcto.");
                // Reiniciar o realizar alguna acción
                puntaje += 10;
                ContadorPuntos.text = $"Puntaje: {puntaje}";
                indiceActual = 0;
            }
        }
        else
        {
            Debug.Log($"Error: Bola {esferaNombre} no está en el orden correcto.");
            // Reiniciar el seguimiento si es necesario
            indiceActual = 0;
        }
    }
        void Start()
        {
            CrearMalla();

            PosicionarBolas();
        }

    // Update is called once per frame
    void Update()
    {
        // Cálculo de las centésimas que dura un frame
        tiempoActual += Time.deltaTime * 100;
        if (tiempoActual >= 100)
        {
            segundosTranscurridos += 1;
            tiempoActual = 0;
        }

        RelojContador.text = $"{segundosTranscurridos:00}:{tiempoActual:00}";
        if (segundosTranscurridos > duracionTablero - 1)
        {
            segundosTranscurridos = 0;
            PosicionarBolas();
        }
        

        }
}


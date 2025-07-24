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
    [SerializeField] private GameObject [] bolasPrefabs; // Array de prefabs de bola
    [SerializeField] [Tooltip("Segundos que duran las esferas en el tablero")][Range(1,10)] private int duracionTablero =5;

    [SerializeField] TextMeshPro RelojContador;
    private float tiempoActual = 0;
    private int segundosTranscurridos = 0;

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
    // crea una función que mande a consola las posiciones de las celdas
    void ImprimirPosicionesCeldas()
    {
        foreach (Vector3 posicion in listaPosicionesCeldas)
        {
            Debug.Log($"Posición de la celda: {posicion}");
        }
    }

    // Start is called before the first frame update

    void PosicionarBolas()
    {
        List<Vector3> posicionesEscogidas = listaPosicionesCeldas
                .OrderBy(x => Random.value)
                .Take(4)
                .ToList();
        for (int i = 0; i < 4; i++)
        {
            GameObject InstanciaBola = Instantiate(bolasPrefabs[i], posicionesEscogidas[i], quaternion.identity);
        }
    }
    void Start()
    {
        CrearMalla();
        ImprimirPosicionesCeldas();
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
            
        } 

    }
}


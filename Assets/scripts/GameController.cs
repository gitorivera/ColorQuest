using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;
//add the random library
using Random = UnityEngine.Random;
using System.Linq;
using TMPro;
using Unity.VisualScripting;

public class GameController : MonoBehaviour
{

    [SerializeField][Tooltip("Número de celdas por lado (N x N)")][Range(2, 5)] private int numeroCeldas = 2;
    [SerializeField] private GameObject CeldaPrefab;
    [SerializeField] private float anchoMalla = 1f;
    private readonly float anchoCeldaPrefab = 0.2f;
    private readonly float puntoCentralX = 0f;
    private readonly float puntoCentralY = 1f;
    private readonly float puntoCentralZ = 1.5f;
    private float posicionInicialX;
    private float posicionInicialY;
    private float posicionInicialZ;
    private GameObject InstanciaDeCelda;
    private List<Vector3> listaPosicionesCeldas = new List<Vector3>();
    private List<Vector3> listaPosicionesBolas = new List<Vector3>();

    private int maxNumeroBolas;

    [Header("Configuración de las Esferas")]
    [SerializeField] private GameObject [] bolasPrefabs; // Array de prefabs de bola

    private List<GameObject> instanciaBolas = new List<GameObject>();
    [SerializeField] [Tooltip("Segundos que duran las esferas en el tablero")][Range(1,10)] private int duracionTablero =5;

    [SerializeField] private TextMeshPro RelojContador;
     //[SerializeField] private TextMeshPro ContadorPuntos; 
    private float tiempoActual = 0;
    private int segundosTranscurridos = 0;

    private int indiceActual = 0; // Track the current index for sphere clicks

// instancia publica para que el método ClickEsfera pueda acceder a ella
    public static GameController Instance;
   
    private int puntaje = 0;

    private TextMeshPro ContadorPuntos;

    private bool destruirEsferas = false;
    [SerializeField]
    private GameObject confetiCorazonesPrefab;
    [SerializeField]
    private GameObject confetiEstrellasPrefab;
    [SerializeField]
    private GameObject confetiMariposasPrefab;

    [SerializeField]
    private GameObject panelSecuenciaPrefab;

// Elementos de la barra de progreso
    [SerializeField]
    private GameObject cuadroBarraProgreso66Prefab;
    [SerializeField]
    private GameObject cuadroBarraProgreso56Prefab;
    [SerializeField]
    private GameObject cuadroBarraProgreso46Prefab;
    [SerializeField]
    private GameObject cuadroBarraProgreso36Prefab;
    [SerializeField]
    private GameObject cuadroBarraProgreso26Prefab;
    [SerializeField]
    private GameObject cuadroBarraProgreso16Prefab;
    [SerializeField]    
    private GameObject cuadroBarraProgreso00Prefab;

   

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
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

        InstanciaDeCelda = Instantiate(CeldaPrefab, new Vector3(posicionInicialX, posicionInicialY, posicionInicialZ), quaternion.identity);
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

        maxNumeroBolas = numeroCeldas * numeroCeldas;
        // Destruir esferas anteriores si es necesario
        if (destruirEsferas)
        {
            foreach (var bola in instanciaBolas)
            {
                Destroy(bola);
            }
            instanciaBolas.Clear();
            destruirEsferas = false;
        }   

        // Ubicamos las boldas en las posiciones escogidas, si el número de bolas es mayor al máximo permitido, se instancian solo las primeras bolas hasta el límite
        if (bolasPrefabs.Length > maxNumeroBolas)
        {
            for (int i = 0; i < maxNumeroBolas; i++)
            {
                GameObject instanciabola = Instantiate(bolasPrefabs[i], posicionesEscogidas[i], Quaternion.identity);
                instanciaBolas.Add(instanciabola);
            }
        }
        else
        {
            for (int i = 0; i < bolasPrefabs.Length; i++)
            {
                GameObject instanciabola = Instantiate(bolasPrefabs[i], posicionesEscogidas[i], Quaternion.identity);
                instanciaBolas.Add(instanciabola);
            }
        }
    }
    void PosicionarLetreros()
    {
        ContadorPuntos = Instantiate(RelojContador, new Vector3(puntoCentralX+0.2f, puntoCentralY+0.6f, puntoCentralZ), Quaternion.identity);
        ContadorPuntos.text = $"00";
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
                ContadorPuntos.text = $"{puntaje}";
                indiceActual = 0;
                destruirEsferas = true;
                PosicionarBolas();
                // Instanciar confeti basado en la última esfera clickeada
                GameObject confetiEstrella = Instantiate(confetiEstrellasPrefab,new Vector3(0f,2f,2f),Quaternion.identity);
                Destroy(confetiEstrella,2f);
                // GameObject confetiCorazon = Instantiate(confetiCorazonesPrefab,new Vector3(0f,1f,2f),Quaternion.identity);
                // Destroy(confetiCorazon,2f);
                // GameObject confetiEstrella = Instantiate(confetiEstrellasPrefab,new Vector3(0f,1f,2f),Quaternion.identity);
            }
        }
        else
        {
            Debug.Log($"Error: Bola {esferaNombre} no está en el orden correcto.");
            // Reiniciar el seguimiento si es necesario
            indiceActual = 0;
        }
    }

    public bool EsEsferaInstanciada(GameObject objeto)
{
    // Verificar si el objeto está en la lista de esferas instanciadas
    return instanciaBolas.Contains(objeto);
}
    void PosicionarPanelSecuencia()
    {
        panelSecuenciaPrefab = Instantiate(panelSecuenciaPrefab, new Vector3(puntoCentralX, 0.3f, puntoCentralZ), Quaternion.identity);
    }

    void PonerSecuencia()
    {
        GameObject bolaSecuencia1 =Instantiate(bolasPrefabs[0], new Vector3(puntoCentralX-0.25f, 0.3f, puntoCentralZ), Quaternion.identity);
        GameObject bolaSecuencia2 =Instantiate(bolasPrefabs[1], new Vector3(puntoCentralX, 0.3f, puntoCentralZ), Quaternion.identity);
        GameObject bolaSecuencia3 =Instantiate(bolasPrefabs[2], new Vector3(puntoCentralX+0.25f, 0.3f, puntoCentralZ), Quaternion.identity);
        // Aquí puedes implementar la lógica para mostrar la secuencia de colores a seguir
        // Por ejemplo, podrías activar un panel con los colores en el orden correcto durante unos segundos
    }

    void PosicionarCuadroBarraProgreso()
    {
       
        // Aquí puedes implementar la lógica para actualizar la barra de progreso según el tiempo transcurrido
    }
    
    
    void Start()
    {
        CrearMalla();

        PosicionarBolas();
        PosicionarLetreros();
        PosicionarPanelSecuencia();
        PonerSecuencia();
      //  PosicionarCuadroBarraProgreso();
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

      //  RelojContador.text = $"{segundosTranscurridos:00}:{tiempoActual:00}";
        if (segundosTranscurridos > duracionTablero - 1)
        {
            segundosTranscurridos = 0;
            destruirEsferas = true;
            PosicionarBolas();
        }
        

    }


}
using Unity.Mathematics;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
//add the random library
using Random = UnityEngine.Random;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
//using UnityEditor.ShaderGraph.Internal;
//using System.Numerics;

public class GameController : MonoBehaviour
{

    [SerializeField][Tooltip("Número de celdas por lado (N x N)")][Range(2, 5)] private int numeroCeldas = 2;
    [SerializeField] private GameObject CeldaPrefab;
    [SerializeField] private GameObject CeldaRojaPrefab;
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
    private List<GameObject> listaInstanciasCeldas = new List<GameObject>();
    private List<GameObject> SecuenciaBolas = new List<GameObject>();

    private int maxNumeroBolas;

    [Header("Configuración de las Esferas")]
    [SerializeField] private GameObject [] bolasPrefabs; // Array de prefabs de bola

    private List<GameObject> instanciaBolas = new List<GameObject>();
    [SerializeField] [Tooltip("Segundos que duran las esferas en el tablero")][Range(1,10)] private int duracionTablero =5;

    [SerializeField] private TextMeshPro RelojContador;
     //[SerializeField] private TextMeshPro ContadorPuntos; 
    private float tiempoActual = 0;

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
    private List<GameObject> instanciasPanelSecuencia = new List<GameObject>();


// Elementos de la barra de progreso
    [SerializeField]
    private GameObject cuadroBarraProgresoPrefab;
    private GameObject cuadroUno;
    private GameObject cuadroDos;
    private GameObject cuadroTres;
    private GameObject cuadroCuatro;
    private GameObject cuadroCinco;
    private GameObject cuadroSeis;
 
// Variables para controlar el tiempo total de duración del juego
  
    private float  DURACION_TOTAL_NIVEL= 20f; // Duración total del nivel de juego en segundos
   
    private float tiempoTranscurridoJuego = 0f; // Tiempo transcurrido desde el inicio del juego

// variables para la flexibilidad del numero de bolas por tablero
    private int numeroBolasTablero = 3; // Número de bolas a posicionar en el tablero (puede ser ajustado)
    
    private bool nivel1 = true;
    private bool nivel2 = true; 
    private bool nivel3 = true;
    private bool nivel4 = true;
    private bool nivel5 = true;
// elementos para generar las pausas en el juego cuando hay victoria u error
    private bool standBy = false;

    private bool primeraEntradaStandBy =false;
    private float tiempoTotalStandBy = 2.5f;
    private float tiempoActualstandBy =0f;
    private readonly WaitForSeconds esperaHaptic = new(0.4f);
    private readonly WaitForSeconds esperaPostHaptic = new(1.6f);

    private GameObject instanciaPanelActivo;

// elementos para el logger
    private float tiempoSpawnTablero   = 0f;
    private float standByStartRealtime = 0f;
    private float tiempoUltimoAcierto  = 0f;
    private int   erroresTablero       = 0;
    private int   erroresSecuencia     = 0;
    private int   tablerosCompletados  = 0;


    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            enabled = false;   // impide que Start() corra en el duplicado
            Destroy(gameObject);
        }

    }

    void CrearMalla(int Celdas)
    {
        //Calculo del ancho de la celda para las instancias creadas
        float anchoCelda = (float)anchoMalla / Celdas;

        float factorEscala = (float)anchoCelda / anchoCeldaPrefab;
        //Posicionamiento de la celda superior izquierda
        int numSaltosIniciales = Celdas - 1;
        float longitudSalto = (float)anchoMalla / (Celdas * 2);
        posicionInicialX = puntoCentralX - (numSaltosIniciales * longitudSalto);
        posicionInicialY = puntoCentralY + (numSaltosIniciales * longitudSalto);
        posicionInicialZ = puntoCentralZ;

        InstanciaDeCelda = Instantiate(CeldaPrefab, new Vector3(posicionInicialX, posicionInicialY, posicionInicialZ), quaternion.identity);
        InstanciaDeCelda.transform.localScale = new Vector3(factorEscala, factorEscala, factorEscala);
        listaInstanciasCeldas.Add(InstanciaDeCelda);
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
                listaInstanciasCeldas.Add(InstanciaDeCelda);
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
                    listaInstanciasCeldas.Add(InstanciaDeCelda);
                    listaPosicionesCeldas.Add(new Vector3(posicionActualX, posicionActualY, posicionActualZ));
                }
                else
                {
                    break;
                }
            }

        }

    }

    void DestruirMalla()
    {
        foreach (var celda in listaInstanciasCeldas)
        {
            Destroy(celda);
        }
        listaInstanciasCeldas.Clear();
        listaPosicionesCeldas.Clear();
    }

    void CrearMallaRoja()
    {
        DestruirMalla();

        float anchoCelda = (float)anchoMalla / numeroCeldas;
        float factorEscala = (float)anchoCelda / anchoCeldaPrefab;
        int numSaltosIniciales = numeroCeldas - 1;
        float longitudSalto = (float)anchoMalla / (numeroCeldas * 2);
        posicionInicialX = puntoCentralX - (numSaltosIniciales * longitudSalto);
        posicionInicialY = puntoCentralY + (numSaltosIniciales * longitudSalto);
        posicionInicialZ = puntoCentralZ;

        float deltaPosicion = 0.95f * anchoCeldaPrefab * factorEscala;
        float posicionActualX = posicionInicialX;
        float posicionActualY = posicionInicialY;
        float posicionActualZ = posicionInicialZ;

        InstanciaDeCelda = Instantiate(CeldaRojaPrefab, new Vector3(posicionInicialX, posicionInicialY, posicionInicialZ), quaternion.identity);
        InstanciaDeCelda.transform.localScale = new Vector3(factorEscala, factorEscala, factorEscala);
        listaInstanciasCeldas.Add(InstanciaDeCelda);
        listaPosicionesCeldas.Add(new Vector3(posicionInicialX, posicionInicialY, posicionInicialZ));

        while (true)
        {
            posicionActualX += deltaPosicion;

            if (posicionActualX < anchoMalla / 2)
            {
                InstanciaDeCelda = Instantiate(CeldaRojaPrefab, new Vector3(posicionActualX, posicionActualY, posicionActualZ), quaternion.identity);
                InstanciaDeCelda.transform.localScale = new Vector3(factorEscala, factorEscala, factorEscala);
                listaInstanciasCeldas.Add(InstanciaDeCelda);
                listaPosicionesCeldas.Add(new Vector3(posicionActualX, posicionActualY, posicionActualZ));
            }
            else
            {
                posicionActualX = posicionInicialX;
                posicionActualY -= deltaPosicion;
                if (posicionActualY > 1 - (anchoMalla / 2))
                {
                    InstanciaDeCelda = Instantiate(CeldaRojaPrefab, new Vector3(posicionActualX, posicionActualY, posicionActualZ), quaternion.identity);
                    InstanciaDeCelda.transform.localScale = new Vector3(factorEscala, factorEscala, factorEscala);
                    listaInstanciasCeldas.Add(InstanciaDeCelda);
                    listaPosicionesCeldas.Add(new Vector3(posicionActualX, posicionActualY, posicionActualZ));
                }
                else
                {
                    break;
                }
            }
        }
    }

    void PosicionarBolas(int numeroBolas)
    {
        List<Vector3> posicionesEscogidas = listaPosicionesCeldas
                .OrderBy(x => Random.value)
                .Take(numeroBolas)
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
        if (numeroBolas > maxNumeroBolas)
        {
            for (int i = 0; i < maxNumeroBolas; i++)
            {
                GameObject instanciabola = Instantiate(bolasPrefabs[i], posicionesEscogidas[i], Quaternion.identity);
                instanciaBolas.Add(instanciabola);
            }
        }
        else
        {
            for (int i = 0; i < numeroBolas; i++)
            {
                GameObject instanciabola = Instantiate(bolasPrefabs[i], posicionesEscogidas[i], Quaternion.identity);
                instanciaBolas.Add(instanciabola);
            }
        }
        // ── EventLogger ──
        tiempoSpawnTablero  = Time.realtimeSinceStartup;
        tiempoUltimoAcierto = tiempoSpawnTablero;
        erroresTablero = 0;
        if (SecuenciaBolas.Count >= 3)
        {
            EventLogger.Instance.NextBoardId();
            var secIds = SecuenciaBolas
                .Select(s => s.name.Replace("(Clone)", "").Trim()).ToList();
            var todasIds = instanciaBolas
                .Select(b => b.name.Replace("(Clone)", "").Trim()).ToList();
            var distractorIds = todasIds.Where(id => !secIds.Contains(id)).ToList();
            var posiciones = instanciaBolas
                .Select(b => b.transform.position).ToList();
            EventLogger.Instance.BoardSpawn(secIds, distractorIds, posiciones, duracionTablero);
            if (UdpSender.Instance != null) UdpSender.Instance.Send(UdpSender.CODE_BOARD_SPAWN);
        }
        // ── fin EventLogger ──
    }
    void PosicionarLetreros()
    {
        ContadorPuntos = Instantiate(RelojContador, new Vector3(puntoCentralX+0.2f, puntoCentralY+0.6f, puntoCentralZ), Quaternion.identity);
        ContadorPuntos.text = $"00";
    }
    public bool EsferaClickeada(GameObject esfera)
    {
        string esferaNombre = esfera.name.Replace("(Clone)", "").Trim();

        if (SecuenciaBolas[indiceActual].name == esferaNombre)
        {
            Debug.Log($"¡Correcto! Bola {esferaNombre} clickeada en el orden correcto.");

            long rtBoard = (long)((Time.realtimeSinceStartup - tiempoSpawnTablero) * 1000f);
            long rtPrev  = (long)((Time.realtimeSinceStartup - tiempoUltimoAcierto) * 1000f);
            string expectedId = SecuenciaBolas[indiceActual].name.Replace("(Clone)", "").Trim();

            // Loguear ANTES de incrementar
            EventLogger.Instance.SphereHitCorrect(
                esferaNombre, indiceActual, expectedId, rtBoard, rtPrev, indiceActual);
            indiceActual++;
            tiempoUltimoAcierto = Time.realtimeSinceStartup;

            if (indiceActual >= 3)
            {
                Debug.Log("¡Felicidades! Has clickeado todas las bolas en el orden correcto.");
                tablerosCompletados++;
                int puntajeAnterior = puntaje;
                puntaje += 10;
                ContadorPuntos.text = $"{puntaje}";

                EventLogger.Instance.ScoreUpdate(puntajeAnterior, puntaje);
                EventLogger.Instance.SequenceCompleted(10, puntaje, rtBoard, erroresSecuencia);
                if (UdpSender.Instance != null) UdpSender.Instance.Send(UdpSender.CODE_SEQ_COMPLETED);
                erroresSecuencia = 0;

                indiceActual = 0;
                destruirEsferas = true;
                PosicionarBolas(numeroBolasTablero);
                GameObject confetiEstrella = Instantiate(confetiEstrellasPrefab, new Vector3(0f,2f,2f), Quaternion.identity);
                Destroy(confetiEstrella, 2f);
                tiempoActual = 0;
            }
            return true;
        }
        else
        {
            Debug.Log($"Error: Bola {esferaNombre} no está en el orden correcto.");
            string expectedId = SecuenciaBolas[indiceActual].name.Replace("(Clone)", "").Trim();
            long rt = (long)((Time.realtimeSinceStartup - tiempoSpawnTablero) * 1000f);

            erroresTablero++;
            erroresSecuencia++;
            EventLogger.Instance.SphereHitIncorrect(esferaNombre, expectedId, indiceActual, rt);
            EventLogger.Instance.BoardFailed(indiceActual, esferaNombre, expectedId, rt);
            EventLogger.Instance.ErrorPauseEnter(esferaNombre, expectedId);

            standByStartRealtime = Time.realtimeSinceStartup;
            tiempoActual = 0;
            indiceActual = 0;
            standBy = true;
            primeraEntradaStandBy = true;
            return false;
        }
    }

    public bool EsEsferaInstanciada(GameObject objeto)
{
    // Verificar si el objeto está en la lista de esferas instanciadas
    return instanciaBolas.Contains(objeto);
}
    public long GetRTSinceBoardSpawn()
    {
        return (long)((Time.realtimeSinceStartup - tiempoSpawnTablero) * 1000f);
    }
    void PosicionarPanelSecuencia()
    {
        instanciaPanelActivo = Instantiate(panelSecuenciaPrefab, new Vector3(puntoCentralX,puntoCentralY*0.3f, puntoCentralZ), Quaternion.identity);
    }

void PonerSecuencia()
{
    // 1. Destruimos las bolas del panel del nivel anterior (si existen)
    foreach (var bola in instanciasPanelSecuencia)
    {
        Destroy(bola);
    }
    instanciasPanelSecuencia.Clear(); // Vaciamos la lista

    // 2. Instanciamos las 3 bolas de la nueva secuencia
    GameObject bolaSecuencia1 = Instantiate(SecuenciaBolas[0], new Vector3(puntoCentralX - 0.25f, 0.3f, puntoCentralZ), Quaternion.identity);
    GameObject bolaSecuencia2 = Instantiate(SecuenciaBolas[1], new Vector3(puntoCentralX, 0.3f, puntoCentralZ), Quaternion.identity);
    GameObject bolaSecuencia3 = Instantiate(SecuenciaBolas[2], new Vector3(puntoCentralX + 0.25f, 0.3f, puntoCentralZ), Quaternion.identity);

    // 3. Las guardamos en la lista para poder destruirlas en el siguiente nivel
    instanciasPanelSecuencia.Add(bolaSecuencia1);
    instanciasPanelSecuencia.Add(bolaSecuencia2);
    instanciasPanelSecuencia.Add(bolaSecuencia3);

    if (EventLogger.Instance != null)
    {
        var ids = SecuenciaBolas.Select(s => s.name.Replace("(Clone)", "").Trim()).ToList();
        EventLogger.Instance.SequenceShown(ids);
    }
}

    void BarajarBolasPrefabs()
    {
        if (bolasPrefabs == null || bolasPrefabs.Length < 2)
        {
            return;
        }

        // Fisher-Yates: in each step choose one random element from [0..i].
        for (int i = bolasPrefabs.Length - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            (bolasPrefabs[i], bolasPrefabs[randomIndex]) = (bolasPrefabs[randomIndex], bolasPrefabs[i]);
        }

        SecuenciaBolas.Clear();
      // res es el número de esferas que se busca en la secuencia.
        for (int i = 0; i < 3; i++)
        {
            SecuenciaBolas.Add(bolasPrefabs[i]);
        }
        Debug.Log("bolas 1" + SecuenciaBolas[0].name);
        Debug.Log("bolas 2" + SecuenciaBolas[1].name);
        Debug.Log("bolas 3" + SecuenciaBolas[2].name);
    }

    void InicializarSecuenciaBolas()
    {
        for (int i = 0; i < 3; i++)
        {
            SecuenciaBolas.Add(bolasPrefabs[i]);
        }
    }   
    void PosicionarCuadroBarraProgreso()
    {
        Vector3 posicionUno = new Vector3(puntoCentralX - 0.45f, puntoCentralY + 0.8f, puntoCentralZ);
        Vector3 posicionDos = new Vector3(puntoCentralX -0.27f, puntoCentralY + 0.8f, puntoCentralZ);
        Vector3 posicionTres = new Vector3(puntoCentralX -0.09f, puntoCentralY + 0.8f, puntoCentralZ);
        Vector3 posicionCuatro = new Vector3(puntoCentralX +0.09f, puntoCentralY + 0.8f, puntoCentralZ);
        Vector3 posicionCinco = new Vector3(puntoCentralX +0.27f, puntoCentralY + 0.8f, puntoCentralZ);
        Vector3 posicionSeis = new Vector3(puntoCentralX +0.45f, puntoCentralY + 0.8f, puntoCentralZ);
       
       
       
       //int porcentajeRestante = (int)(1f - progreso)*100;

       
        cuadroUno = Instantiate(cuadroBarraProgresoPrefab, posicionUno, Quaternion.identity);
        cuadroDos = Instantiate(cuadroBarraProgresoPrefab, posicionDos, Quaternion.identity);
        cuadroTres = Instantiate(cuadroBarraProgresoPrefab, posicionTres, Quaternion.identity);
        cuadroCuatro = Instantiate(cuadroBarraProgresoPrefab, posicionCuatro, Quaternion.identity);
        cuadroCinco = Instantiate(cuadroBarraProgresoPrefab, posicionCinco, Quaternion.identity);
        cuadroSeis = Instantiate(cuadroBarraProgresoPrefab, posicionSeis, Quaternion.identity);

        MeshRenderer renderer = cuadroUno.GetComponent<MeshRenderer>();  
       Material mat = renderer.material; // Obtenemos la instancia


    
    }
    
    

    // Update is called once per frame
    
    void actualizarBarraProgreso(float progreso)
    {
       // progreso de 83% a 100% es verde, de 66% a 83% es naranja, de 50% a 
       // 66% es amarillo, de 33% a 50% es naranja, de 16% a 33% es rojo, 
       // de 0% a 16% es rojo intenso
        if (progreso > 0.83f)
        {
            // Cambiar el color a verde
            cuadroUno.GetComponent<MeshRenderer>().material.color = new Color32(0, 255, 0, 255); // Verde
            cuadroDos.GetComponent<MeshRenderer>().material.color = new Color32(0, 255, 0, 255); // Verde
            cuadroTres.GetComponent<MeshRenderer>().material.color = new Color32(0, 255, 0, 255); // Verde
            cuadroCuatro.GetComponent<MeshRenderer>().material.color = new Color32(0, 255, 0, 255); // Verde
            cuadroCinco.GetComponent<MeshRenderer>().material.color = new Color32(0, 255, 0, 255); // Verde
            cuadroSeis.GetComponent<MeshRenderer>().material.color = new Color32(0, 255, 0, 255); // Verde
        }
        else if (progreso <= 0.83f && progreso > 0.66f)
        {
            // Cambiar el color a naranja
            cuadroUno.GetComponent<MeshRenderer>().material.color = new Color32(154, 255, 0, 255); // Naranja
            cuadroDos.GetComponent<MeshRenderer>().material.color = new Color32(154, 255, 0, 255); // Naranja
            cuadroTres.GetComponent<MeshRenderer>().material.color = new Color32(154, 255, 0, 255); // Naranja
            cuadroCuatro.GetComponent<MeshRenderer>().material.color = new Color32(154, 255, 0, 255); // Naranja
            cuadroCinco.GetComponent<MeshRenderer>().material.color = new Color32(154, 255, 0, 255); // Naranja
            cuadroSeis.GetComponent<MeshRenderer>().material.color = new Color32(255, 255, 255, 25); // Blanco
        }
      else if (progreso <= 0.66f && progreso > 0.5f)
        {
            // Cambiar el color a amarillo
            cuadroUno.GetComponent<MeshRenderer>().material.color = new Color32(200, 255, 0, 255); // Amarillo
            cuadroDos.GetComponent<MeshRenderer>().material.color = new Color32(200, 255, 0, 255); // Amarillo
            cuadroTres.GetComponent<MeshRenderer>().material.color = new Color32(200, 255, 0, 255); // Amarillo
            cuadroCuatro.GetComponent<MeshRenderer>().material.color = new Color32(200, 255, 0, 255); // Amarillo
            cuadroCinco.GetComponent<MeshRenderer>().material.color = new Color32(255, 255, 255, 25); // Blanco
            cuadroSeis.GetComponent<MeshRenderer>().material.color = new Color32(255, 255, 255, 25); // Blanco
        }
        else if (progreso <= 0.5f && progreso > 0.33f)
        {
            // Cambiar el color a naranja
            cuadroUno.GetComponent<MeshRenderer>().material.color = new Color32(235, 255, 0, 255); // Naranja
            cuadroDos.GetComponent<MeshRenderer>().material.color = new Color32(235, 255, 0, 255); // Naranja
            cuadroTres.GetComponent<MeshRenderer>().material.color = new Color32(235, 255, 0, 255); // Naranja
            cuadroCuatro.GetComponent<MeshRenderer>().material.color = new Color32(255, 255, 255, 25); // Blanca
            cuadroCinco.GetComponent<MeshRenderer>().material.color = new Color32(255, 255, 255, 25); // Blanca
            cuadroSeis.GetComponent<MeshRenderer>().material.color = new Color32(255, 255, 255, 25); // Blanca
        }
        else if (progreso <=0.33f && progreso >0.16f)
        {
             // Cambiar el color a rojo
            cuadroUno.GetComponent<MeshRenderer>().material.color = new Color32(255, 180, 0, 255); // Rojo
            cuadroDos.GetComponent<MeshRenderer >().material.color = new Color32(255, 180, 0, 255); // Rojo
            cuadroTres.GetComponent<MeshRenderer>().material.color = new Color32(255, 255, 255, 25); // Blanca
            cuadroCuatro.GetComponent<MeshRenderer>().material.color = new Color32(255, 255, 255, 25); // Blanca
            cuadroCinco.GetComponent<MeshRenderer>().material.color = new Color32(255, 255, 255, 25); // Blanca
            cuadroSeis.GetComponent<MeshRenderer>().material.color = new Color32(255, 255, 255, 25); // Blanca  
        
    }else if (progreso <= 0.16f)
        {
             // Cambiar el color a rojo intenso
            cuadroUno.GetComponent<MeshRenderer>().material.color = new Color32(255, 0, 0, 255); // Rojo intenso
            cuadroDos.GetComponent<MeshRenderer>().material.color = new Color32(255, 255, 255, 25); // Rojo intenso
            cuadroTres.GetComponent<MeshRenderer>().material.color = new Color32(255, 255, 255, 25); // Blanca
            cuadroCuatro.GetComponent<MeshRenderer>().material.color = new Color32(255, 255, 255, 25); // Blanca
            cuadroCinco.GetComponent<MeshRenderer>().material.color = new Color32(255, 255, 255, 25); // Blanca
            cuadroSeis.GetComponent<MeshRenderer>().material.color = new Color32(255, 255, 255, 25); // Blanca  
        }
    }
    
        void Start()
    {
        tiempoSpawnTablero  = Time.realtimeSinceStartup;
        tiempoUltimoAcierto = Time.realtimeSinceStartup;
        EventLogger.Instance.StartSession(
            SystemInfo.deviceModel,
            Application.version,
            "RTouch",
            DURACION_TOTAL_NIVEL,
            duracionTablero
        );
        CrearMalla(2);
        ////BarajarBolasPrefabs();
        PosicionarBolas(numeroBolasTablero);
        PosicionarLetreros();
        PosicionarPanelSecuencia();
        InicializarSecuenciaBolas();
        //PonerSecuencia();
        PosicionarCuadroBarraProgreso();
    }
    void AjustarNivel(float tiempo)
    {
        if (tiempo<DURACION_TOTAL_NIVEL)
        {
            // nivel 1
            //bool nivel1 = true;
            numeroBolasTablero = 3;
            numeroCeldas =2;
            if (nivel1)
            {
                EventLogger.Instance.SetContext(1, numeroCeldas, numeroBolasTablero - 3);
                DestruirMalla();
                CrearMalla(numeroCeldas);
                destruirEsferas = true;
                BarajarBolasPrefabs();
                PonerSecuencia();
                EventLogger.Instance.LevelStart();
                if (UdpSender.Instance != null) UdpSender.Instance.Send(UdpSender.CODE_LEVEL_START);
                nivel1 = false;
                PosicionarBolas(numeroBolasTablero);
            }
        }else if (tiempo>=DURACION_TOTAL_NIVEL && tiempo<DURACION_TOTAL_NIVEL * 2)
        {
            // nivel 2
            //bool nivel2 = true;
            numeroBolasTablero = 4;
            numeroCeldas =3;
            if (nivel2)
            {
                EventLogger.Instance.BoardTimeout(indiceActual, indiceActual / 3f);
                indiceActual = 0;
                erroresSecuencia = 0;
                tiempoActual = 0;
                EventLogger.Instance.SetContext(2, numeroCeldas, numeroBolasTablero - 3);
                DestruirMalla();
                CrearMalla(numeroCeldas);
                destruirEsferas = true;
                BarajarBolasPrefabs();
                PonerSecuencia();
                EventLogger.Instance.LevelStart();
                if (UdpSender.Instance != null) UdpSender.Instance.Send(UdpSender.CODE_LEVEL_START);
                nivel2 = false;
                PosicionarBolas(numeroBolasTablero);
            }

        }else if (tiempo>=DURACION_TOTAL_NIVEL * 2 && tiempo<DURACION_TOTAL_NIVEL * 3)
        {
            // nivel 3
            //bool nivel3 = true;
            numeroBolasTablero = 5;
            numeroCeldas =4;
            if (nivel3)
            {
                EventLogger.Instance.BoardTimeout(indiceActual, indiceActual / 3f);
                indiceActual = 0;
                erroresSecuencia = 0;
                tiempoActual = 0;
                EventLogger.Instance.SetContext(3, numeroCeldas, numeroBolasTablero - 3);
                DestruirMalla();
                CrearMalla(numeroCeldas);
                destruirEsferas = true;
                BarajarBolasPrefabs();
                PonerSecuencia();
                EventLogger.Instance.LevelStart();
                if (UdpSender.Instance != null) UdpSender.Instance.Send(UdpSender.CODE_LEVEL_START);
                nivel3 = false;
                PosicionarBolas(numeroBolasTablero);
            }
        }else if (tiempo>=DURACION_TOTAL_NIVEL * 3 && tiempo<DURACION_TOTAL_NIVEL * 4)
        {
            // nivel 4
            //bool nivel4 = true;
            numeroBolasTablero = 6;
            numeroCeldas =5;
            if (nivel4)
            {
                EventLogger.Instance.BoardTimeout(indiceActual, indiceActual / 3f);
                indiceActual = 0;
                erroresSecuencia = 0;
                tiempoActual = 0;
                EventLogger.Instance.SetContext(4, numeroCeldas, numeroBolasTablero - 3);
                DestruirMalla();
                CrearMalla(numeroCeldas);
                destruirEsferas = true;
                BarajarBolasPrefabs();
                PonerSecuencia();
                EventLogger.Instance.LevelStart();
                if (UdpSender.Instance != null) UdpSender.Instance.Send(UdpSender.CODE_LEVEL_START);
                nivel4 = false;
                PosicionarBolas(numeroBolasTablero);
            }
        }else if (tiempo>=DURACION_TOTAL_NIVEL * 4 && tiempo<DURACION_TOTAL_NIVEL * 5)
        {
            // nivel 5
            
            //bool nivel5 = true;
            numeroBolasTablero = 7;
            numeroCeldas =6;
            if (nivel5)
            {
                EventLogger.Instance.BoardTimeout(indiceActual, indiceActual / 3f);
                indiceActual = 0;
                erroresSecuencia = 0;
                tiempoActual = 0;
                EventLogger.Instance.SetContext(5, numeroCeldas, numeroBolasTablero - 3);
                DestruirMalla();
                CrearMalla(numeroCeldas);
                destruirEsferas = true;
                BarajarBolasPrefabs();
                PonerSecuencia();
                EventLogger.Instance.LevelStart();
                if (UdpSender.Instance != null) UdpSender.Instance.Send(UdpSender.CODE_LEVEL_START);
                nivel5 = false;
                PosicionarBolas(numeroBolasTablero);
            }

        }else
        {
            EventLogger.Instance.BoardTimeout(indiceActual, indiceActual / 3f);
            indiceActual = 0;
            EventLogger.Instance.EndSession(puntaje, tablerosCompletados);
            if (UdpSender.Instance != null) UdpSender.Instance.Send(UdpSender.CODE_GAME_END);
            tiempoTranscurridoJuego = 0f;
            nivel1 = true;
            nivel2 = true;
            nivel3 = true;
            nivel4 = true;
            nivel5 = true;
        }
    }
private IEnumerator PutRedGridAlert()
    {
        if (CeldaRojaPrefab != null)
        {
            destruirEsferas = true;
            foreach (var bola in instanciaBolas) { Destroy(bola); }
            instanciaBolas.Clear();
            DestruirMalla();
            OVRInput.SetControllerVibration(0.5f, 1f, OVRInput.Controller.RTouch);
            OVRInput.SetControllerVibration(0.5f, 1f, OVRInput.Controller.LTouch);
            EventLogger.Instance.HapticFeedback(0.5f, 400, "sequence_error");
            CrearMallaRoja();
            yield return esperaHaptic;
            OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch);
            OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.LTouch);
            yield return esperaPostHaptic;
            DestruirMalla();
            EventLogger.Instance.ErrorPauseExit((long)(tiempoTotalStandBy * 1000f));
            CrearMalla(numeroCeldas);
        }
        else
        {
            Debug.LogWarning("El panel activo es nulo");
            yield return null;
        }
    }

    void Update()
    {
        // Tiempo total del juego
        tiempoTranscurridoJuego += Time.deltaTime;
        if (standBy)
        {
            tiempoActualstandBy+=Time.deltaTime;
            if (tiempoActualstandBy<=tiempoTotalStandBy)
            {
                if (primeraEntradaStandBy)
                {
                    destruirEsferas = true;
                    foreach (var bola in instanciaBolas)
                    {
                        Destroy(bola);
                    }
                    instanciaBolas.Clear();
                    primeraEntradaStandBy = false;
                    StartCoroutine(PutRedGridAlert());
                }
                tiempoActual = 0f;
            }
            else
            {
                tiempoActualstandBy = 0f;
                standBy = false;
                // Run AjustarNivel first so a pending level transition handles the board spawn.
                // Only spawn manually when no level transition occurred (nivel flags unchanged).
                bool sn1 = nivel1, sn2 = nivel2, sn3 = nivel3, sn4 = nivel4, sn5 = nivel5;
                AjustarNivel(tiempoTranscurridoJuego);
                bool levelChanged = sn1 != nivel1 || sn2 != nivel2 || sn3 != nivel3
                                    || sn4 != nivel4 || sn5 != nivel5;
                if (!levelChanged)
                    PosicionarBolas(numeroBolasTablero);
            }
        }
        else
        {
            AjustarNivel(tiempoTranscurridoJuego);
        
        }
        tiempoActual += Time.deltaTime;
     
       
         
        //porcentaje de tablero completado
        float progreso =Mathf.Clamp01(tiempoActual / duracionTablero);
        actualizarBarraProgreso(1-progreso);
        
        


      //  RelojContador.text = $"{segundosTranscurridos:00}:{tiempoActual:00}";
        if (!standBy && GetRTSinceBoardSpawn() > duracionTablero * 1000f)
        {
            EventLogger.Instance.BoardTimeout(indiceActual, indiceActual / 3f);
            tiempoActual = 0;
            destruirEsferas = true;
            indiceActual = 0;
            PosicionarBolas(numeroBolasTablero);
        }
        
        }
    }


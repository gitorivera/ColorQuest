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

    private float  DURACION_TOTAL_NIVEL= 20f; // Duración de CADA nivel (el reloj se reinicia por nivel)

    private float tiempoTranscurridoJuego = 0f; // Tiempo transcurrido dentro del nivel actual

// ── Máquina de estados ──────────────────────────────────────────────
    private enum EstadoJuego { EsperandoPractica, Practicando, EsperandoInicio, MostrandoSecuencia, Jugando, StandByError, Finalizado }
    private EstadoJuego estado = EstadoJuego.EsperandoInicio;

    // El juego sólo procesa disparos contra esferas mientras se está jugando.
    public bool JuegoActivo => estado == EstadoJuego.Jugando;

    // Disparos y resaltado contra esferas: durante el juego y durante la práctica.
    public bool PunteriaActiva => estado == EstadoJuego.Jugando || estado == EstadoJuego.Practicando;

    public bool EnPractica => estado == EstadoJuego.Practicando;

    // True mientras se muestra el botón de inicio y no ha comenzado la sesión.
    public bool EsperandoInicio => estado == EstadoJuego.EsperandoInicio;

    // True mientras se muestra el botón de práctica, antes del botón de inicio.
    public bool EsperandoPractica => estado == EstadoJuego.EsperandoPractica;

    public bool EsBotonPractica(GameObject obj)
    {
        if (obj == null || botonPractica == null) return false;
        return obj == botonPractica || obj.transform.IsChildOf(botonPractica.transform);
    }

    // True si el objeto impactado es el botón de inicio (o parte de su jerarquía),
    // para que el blaster pueda arrancar el juego disparándole.
    public bool EsBotonInicio(GameObject obj)
    {
        if (obj == null || botonInicio == null) return false;
        return obj == botonInicio || obj.transform.IsChildOf(botonInicio.transform);
    }

// ── Niveles como datos ──────────────────────────────────────────────
    private struct ConfigNivel { public int celdas; public int bolas; }
    private static readonly ConfigNivel[] niveles = {
        new ConfigNivel{ celdas = 2, bolas = 3 },
        new ConfigNivel{ celdas = 3, bolas = 4 },
        new ConfigNivel{ celdas = 4, bolas = 5 },
        new ConfigNivel{ celdas = 5, bolas = 6 },
        new ConfigNivel{ celdas = 6, bolas = 7 },
    };
    private int nivelActual = 0;                 // índice 0..4
    private bool finDeNivelPendiente = false;    // el nivel terminó pero esperamos a que el tablero se resuelva

// variables para la flexibilidad del numero de bolas por tablero
    private int numeroBolasTablero = 3; // Número de bolas a posicionar en el tablero (puede ser ajustado)

// ── Presentación de la secuencia en el centro (ajustable en el Inspector) ──
    [SerializeField][Tooltip("Segundos que la secuencia permanece en el centro")] private float duracionPresentacionSecuencia = 3f;
    [SerializeField][Tooltip("Segundos que tarda la secuencia en bajar a su posición")] private float duracionAnimacionBajada = 1f;

// ── Botón físico de inicio (Meta Building Blocks) ──
    [SerializeField][Tooltip("Objeto raíz del botón INICIAR; se desactiva al empezar")] private GameObject botonInicio;

// ── Fase de práctica: familiarización con la puntería antes del juego ──
    [Header("Práctica")]
    [SerializeField][Tooltip("Si está activa, antes del botón INICIAR aparece el botón de práctica")] private bool practicaHabilitada = true;
    [SerializeField][Tooltip("Opcional: botón de práctica colocado en la escena. Si se deja vacío se clona el botón INICIAR")] private GameObject botonPractica;
    [SerializeField] private string textoBotonPractica = "Iniciar práctica";
    [SerializeField][Tooltip("Segundos que dura la práctica")] private float duracionPractica = 45f;
    [SerializeField][Tooltip("Malla N x N sobre la que aparecen los objetivos de práctica")][Range(2, 6)] private int celdasPractica = 4;

    private float tiempoPractica = 0f;
    private int segundoMostradoPractica = -1;
    private Vector3 posicionObjetivoPractica;
    private float tiempoSpawnObjetivoPractica = 0f;
    private int objetivosPractica = 0;
    private int disparosPractica = 0;
    private int aciertosPractica = 0;
    private float sumaTiempoAciertoPractica = 0f;
    private bool practicaRealizada = false;

// elementos para generar las pausas en el juego cuando hay victoria u error
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
        }
        // ── fin EventLogger ──
    }
    void PosicionarLetreros()
    {
        ContadorPuntos = Instantiate(RelojContador, new Vector3(puntoCentralX, puntoCentralY+0.6f, puntoCentralZ), Quaternion.identity);
        // Alineación centrada para que el texto quede siempre centrado sobre puntoCentralX,
        // sin importar su longitud ("00" vs "Secuencia").
        ContadorPuntos.alignment = TextAlignmentOptions.Center;
        ContadorPuntos.text = $"00";
    }
    public void EsferaClickeada(GameObject esfera)
    {
        if (estado == EstadoJuego.Practicando)
        {
            ObjetivoPracticaAcertado(esfera);
            return;
        }

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
                erroresSecuencia = 0;

                GameObject confetiEstrella = Instantiate(confetiEstrellasPrefab, new Vector3(0f,2f,puntoCentralZ), Quaternion.identity);
                Destroy(confetiEstrella, 2f);
                TableroResuelto("completed");
            }
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
            estado = EstadoJuego.StandByError;
            primeraEntradaStandBy = true;
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
    // Y de la posición final (habitual) del panel y las esferas de secuencia.
    private float PanelSecuenciaYFinal => puntoCentralY * 0.3f;   // = 0.3

    // Crea el panel y las 3 esferas de la secuencia a una altura "baseY" dada.
    // Reutilizado tanto para la presentación centrada como para su posición final.
    void CrearPanelYSecuencia(float baseY)
    {
        // 1. Destruimos el panel y las esferas del nivel anterior (si existen)
        if (instanciaPanelActivo != null) Destroy(instanciaPanelActivo);
        foreach (var bola in instanciasPanelSecuencia)
        {
            Destroy(bola);
        }
        instanciasPanelSecuencia.Clear();

        // 2. Instanciamos el panel y las 3 bolas de la nueva secuencia a la altura dada
        instanciaPanelActivo = Instantiate(panelSecuenciaPrefab, new Vector3(puntoCentralX, baseY, puntoCentralZ), Quaternion.identity);

        GameObject bolaSecuencia1 = Instantiate(SecuenciaBolas[0], new Vector3(puntoCentralX - 0.25f, baseY, puntoCentralZ), Quaternion.identity);
        GameObject bolaSecuencia2 = Instantiate(SecuenciaBolas[1], new Vector3(puntoCentralX, baseY, puntoCentralZ), Quaternion.identity);
        GameObject bolaSecuencia3 = Instantiate(SecuenciaBolas[2], new Vector3(puntoCentralX + 0.25f, baseY, puntoCentralZ), Quaternion.identity);

        instanciasPanelSecuencia.Add(bolaSecuencia1);
        instanciasPanelSecuencia.Add(bolaSecuencia2);
        instanciasPanelSecuencia.Add(bolaSecuencia3);
    }

    // Presenta la secuencia en el centro del espacio de juego, la mantiene unos
    // segundos y luego la baja suavemente a su posición habitual. Mientras corre,
    // el estado MostrandoSecuencia congela el reloj de nivel y el timeout de tablero.
    private IEnumerator MostrarSecuenciaNivel()
    {
        estado = EstadoJuego.MostrandoSecuencia;

        // El letrero de puntaje avisa que se está mostrando la nueva secuencia a memorizar
        if (ContadorPuntos != null) ContadorPuntos.text = "Secuencia";

        // Panel + esferas en el CENTRO (la malla permanece oculta durante la presentación)
        CrearPanelYSecuencia(puntoCentralY);

        // Marcador LSL/EEG en el momento en que el niño ve la secuencia
        if (EventLogger.Instance != null)
        {
            var ids = SecuenciaBolas.Select(s => s.name.Replace("(Clone)", "").Trim()).ToList();
            EventLogger.Instance.SequenceShown(ids);
        }

        yield return new WaitForSeconds(duracionPresentacionSecuencia);

        // Animación de bajada: panel y esferas se mueven juntos del centro a la posición final
        float yInicio = puntoCentralY;
        float yFinal  = PanelSecuenciaYFinal;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, duracionAnimacionBajada);
            float y = Mathf.Lerp(yInicio, yFinal, Mathf.Clamp01(t));

            if (instanciaPanelActivo != null)
            {
                Vector3 p = instanciaPanelActivo.transform.position;
                instanciaPanelActivo.transform.position = new Vector3(p.x, y, p.z);
            }
            foreach (var bola in instanciasPanelSecuencia)
            {
                if (bola == null) continue;
                Vector3 p = bola.transform.position;
                bola.transform.position = new Vector3(p.x, y, p.z);
            }
            yield return null;
        }

        // Recién al iniciar el nivel aparece la malla y, sobre ella, el tablero.
        // PosicionarBolas fija tiempoSpawnTablero y emite BoardSpawn, de modo que la
        // presentación no consume el timeout de tablero.
        estado = EstadoJuego.Jugando;
        if (ContadorPuntos != null) ContadorPuntos.text = $"{puntaje}";
        CrearMalla(numeroCeldas);
        tiempoActual = 0f;
        PosicionarBolas(numeroBolasTablero);
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
        // La escena inicial sólo muestra el botón de práctica (o el de inicio si la
        // práctica está deshabilitada). Nada del juego corre ni se emite ningún evento
        // de sesión hasta que el jugador oprime el botón INICIAR.
        if (practicaHabilitada && botonPractica == null) botonPractica = CrearBotonPractica();

        if (practicaHabilitada && botonPractica != null)
        {
            estado = EstadoJuego.EsperandoPractica;
            botonPractica.SetActive(true);
            if (botonInicio != null) botonInicio.SetActive(false);
        }
        else
        {
            MostrarBotonInicio();
        }
    }

    private void MostrarBotonInicio()
    {
        estado = EstadoJuego.EsperandoInicio;
        if (botonInicio != null) botonInicio.SetActive(true);
    }

    // ── Práctica ────────────────────────────────────────────────────────

    // Clona el botón INICIAR en la misma posición (nunca se muestran a la vez)
    // y cambia su etiqueta.
    private GameObject CrearBotonPractica()
    {
        if (botonInicio == null) return null;

        GameObject boton = Instantiate(botonInicio, botonInicio.transform.position, botonInicio.transform.rotation);
        boton.name = "BotonPractica";

        TMP_Text etiqueta = boton.GetComponentInChildren<TMP_Text>(true);
        if (etiqueta != null)
        {
            // La fuente del botón (Roboto de los ejemplos de Meta) no trae tildes;
            // si le falta algún carácter se usa la fuente por defecto de TMP.
            if (etiqueta.font != null && !etiqueta.font.HasCharacters(textoBotonPractica)
                && TMP_Settings.defaultFontAsset != null)
            {
                etiqueta.font = TMP_Settings.defaultFontAsset;
            }
            etiqueta.text = textoBotonPractica;
            // El texto es más largo que "Iniciar": se reduce hasta caber en la etiqueta
            etiqueta.enableAutoSizing = true;
            etiqueta.fontSizeMax = etiqueta.fontSize;
            etiqueta.fontSizeMin = etiqueta.fontSize * 0.4f;
        }
        return boton;
    }

    // Punto de entrada a la práctica: se llama al dispararle al botón de práctica.
    // Un objetivo a la vez, del mismo tamaño que en el juego, sobre una malla
    // celdasPractica x celdasPractica. No abre sesión ni envía marcadores LSL.
    public void IniciarPractica()
    {
        if (estado != EstadoJuego.EsperandoPractica) return;
        if (botonPractica != null) botonPractica.SetActive(false);

        tiempoPractica = 0f;
        segundoMostradoPractica = -1;
        objetivosPractica = 0;
        disparosPractica = 0;
        aciertosPractica = 0;
        sumaTiempoAciertoPractica = 0f;

        if (ContadorPuntos == null) PosicionarLetreros();

        CrearMalla(celdasPractica);
        estado = EstadoJuego.Practicando;
        GenerarObjetivoPractica();
        Debug.Log($"Práctica iniciada: {duracionPractica} s, malla {celdasPractica}x{celdasPractica}");
    }

    // Blaster lo llama en cada disparo durante la práctica (acierte o no).
    public void RegistrarDisparoPractica()
    {
        if (estado == EstadoJuego.Practicando) disparosPractica++;
    }

    private void GenerarObjetivoPractica()
    {
        // Celda aleatoria, distinta de la del objetivo anterior
        int n = listaPosicionesCeldas.Count;
        int idx = Random.Range(0, n);
        if (n > 1 && objetivosPractica > 0 && listaPosicionesCeldas[idx] == posicionObjetivoPractica)
        {
            idx = (idx + Random.Range(1, n)) % n;
        }
        posicionObjetivoPractica = listaPosicionesCeldas[idx];

        GameObject prefab = bolasPrefabs[Random.Range(0, bolasPrefabs.Length)];
        GameObject objetivo = Instantiate(prefab, posicionObjetivoPractica, Quaternion.identity);
        instanciaBolas.Add(objetivo);   // así Blaster la reconoce como esfera

        objetivosPractica++;
        tiempoSpawnObjetivoPractica = Time.realtimeSinceStartup;
    }

    private void ObjetivoPracticaAcertado(GameObject esfera)
    {
        aciertosPractica++;
        sumaTiempoAciertoPractica += Time.realtimeSinceStartup - tiempoSpawnObjetivoPractica;
        instanciaBolas.Remove(esfera);   // Blaster la destruye después de esta llamada
        GenerarObjetivoPractica();
    }

    private void ActualizarPractica()
    {
        tiempoPractica += Time.deltaTime;

        // Cuenta regresiva en el letrero (sólo se reescribe cuando cambia el segundo)
        int restante = Mathf.Max(0, Mathf.CeilToInt(duracionPractica - tiempoPractica));
        if (restante != segundoMostradoPractica && ContadorPuntos != null)
        {
            segundoMostradoPractica = restante;
            ContadorPuntos.text = $"Práctica {restante}";
        }

        if (tiempoPractica >= duracionPractica) FinalizarPractica();
    }

    private void FinalizarPractica()
    {
        foreach (var bola in instanciaBolas) Destroy(bola);
        instanciaBolas.Clear();
        DestruirMalla();

        practicaRealizada = true;
        if (ContadorPuntos != null) ContadorPuntos.text = "";
        Debug.Log($"Práctica terminada: {aciertosPractica} aciertos / {disparosPractica} disparos");

        // Vuelve a aparecer el botón INICIAR, igual que antes de existir la práctica
        MostrarBotonInicio();
    }

    // Punto de entrada al juego: se llama desde el evento del botón físico (poke).
    public void IniciarJuego()
    {
        if (estado != EstadoJuego.EsperandoInicio) return;

        // Reiniciar contadores de sesión (permite sesiones experimentales repetibles)
        puntaje = 0;
        tablerosCompletados = 0;
        erroresSecuencia = 0;
        indiceActual = 0;
        tiempoTranscurridoJuego = 0f;
        finDeNivelPendiente = false;

        tiempoSpawnTablero  = Time.realtimeSinceStartup;
        tiempoUltimoAcierto = Time.realtimeSinceStartup;

        // La sesión EEG/LSL empieza con el gesto del jugador
        EventLogger.Instance.StartSession(
            SystemInfo.deviceModel,
            Application.version,
            "RTouch",
            DURACION_TOTAL_NIVEL,
            duracionTablero
        );

        // La práctica ocurre antes de abrir la sesión: su resumen (línea base de
        // puntería) se registra justo después de session_start.
        if (practicaRealizada)
        {
            long tiempoMedioAciertoMs = aciertosPractica > 0
                ? (long)(sumaTiempoAciertoPractica / aciertosPractica * 1000f) : 0;
            EventLogger.Instance.PracticeSummary(duracionPractica, celdasPractica,
                objetivosPractica, disparosPractica, aciertosPractica, tiempoMedioAciertoMs);
        }

        // TET: primer destello + "sync_1" + "inicio partida" en este mismo cuadro
        SyncFlash.Asegurar().IniciarPartida();

        // Elementos pasivos de HUD (sólo una vez, aunque se repita la sesión)
        if (ContadorPuntos == null) PosicionarLetreros();
        else ContadorPuntos.text = "00";
        if (cuadroUno == null) PosicionarCuadroBarraProgreso();

        if (botonInicio != null) botonInicio.SetActive(false);

        nivelActual = 0;
        EntrarANivel(0);
    }

    // Configura el nivel idx, crea la malla y arranca la presentación de la secuencia.
    private void EntrarANivel(int idx)
    {
        numeroCeldas       = niveles[idx].celdas;
        numeroBolasTablero = niveles[idx].bolas;

        EventLogger.Instance.SetContext(idx + 1, numeroCeldas, numeroBolasTablero - 3);

        // La malla NO se crea aquí: permanece oculta durante la presentación de la
        // secuencia y se crea al final de la corrutina, al iniciar el nivel.
        DestruirMalla();

        // Limpiar las esferas del tablero anterior de inmediato: la presentación de la
        // secuencia se muestra sobre el espacio vacío (PosicionarBolas se difiere al final).
        foreach (var bola in instanciaBolas) Destroy(bola);
        instanciaBolas.Clear();
        destruirEsferas = false;

        indiceActual = 0;
        erroresSecuencia = 0;
        BarajarBolasPrefabs();
        EventLogger.Instance.LevelStart();

        // La corrutina termina llamando PosicionarBolas (spawn del tablero)
        StartCoroutine(MostrarSecuenciaNivel());
    }

    // Único punto de resolución de tablero: se llama cuando el tablero en curso
    // se completa, agota (timeout) o se recupera de un error. Si el nivel ya
    // terminó, avanza; si no, genera un nuevo tablero del mismo nivel.
    private void TableroResuelto(string motivo)
    {
        if (finDeNivelPendiente) { AvanzarNivel(); return; }

        destruirEsferas = true;
        indiceActual = 0;
        tiempoActual = 0;
        PosicionarBolas(numeroBolasTablero);
    }

    private void AvanzarNivel()
    {
        finDeNivelPendiente = false;
        tiempoTranscurridoJuego = 0f;   // el reloj de nivel es POR NIVEL, ya no acumulado
        nivelActual++;
        if (nivelActual >= niveles.Length) { FinalizarSesion(); return; }
        EntrarANivel(nivelActual);
    }

    private void FinalizarSesion()
    {
        // Al llegar aquí el tablero ya fue resuelto por TableroResuelto (que emitió su
        // evento correspondiente), así que sólo cerramos la sesión, una sola vez.
        indiceActual = 0;

        // TET: último destello + "sync_N" + "fin partida", antes de la pantalla final
        SyncFlash.Instance?.FinalizarPartida();
        EventLogger.Instance.EndSession(puntaje, tablerosCompletados);

        // Limpiar malla, esferas y panel de secuencia (el letrero de puntaje se conserva)
        DestruirMalla();
        foreach (var bola in instanciaBolas) Destroy(bola);
        instanciaBolas.Clear();
        destruirEsferas = false;
        if (instanciaPanelActivo != null) Destroy(instanciaPanelActivo);
        foreach (var bola in instanciasPanelSecuencia) Destroy(bola);
        instanciasPanelSecuencia.Clear();

        estado = EstadoJuego.Finalizado;

        // Pantalla final: el puntaje total se muestra cuando el último destello ya se
        // apagó. En la prueba exp006 (2026-10-14) el texto «Puntaje total» apareció en el
        // mismo cuadro que el destello final y tapaba parte del cuadrado blanco.
        StartCoroutine(MostrarPuntajeFinal());

        // Celebración de fin: confeti durante 10 segundos, uno por segundo.
        StartCoroutine(LanzarConfetiFinal());
    }

    // Muestra el puntaje total en el centro del tablero. No se reinicia nada ni se
    // vuelve al botón de inicio; ésta es la pantalla de fin.
    private IEnumerator MostrarPuntajeFinal()
    {
        float espera = SyncFlash.Instance != null ? SyncFlash.Instance.DuracionSegundos + 0.1f : 0f;
        if (espera > 0f) yield return new WaitForSecondsRealtime(espera);

        if (ContadorPuntos != null)
        {
            ContadorPuntos.transform.position = new Vector3(puntoCentralX, puntoCentralY, puntoCentralZ);
            ContadorPuntos.alignment = TextAlignmentOptions.Center;
            ContadorPuntos.text = $"Puntaje total: {puntaje}";
        }
    }

    // TET: ¿puede salir ahora un destello intermedio sin quedar a menos de
    // "margenSegundos" del destello final? El juego sólo termina cuando el reloj
    // del ÚLTIMO nivel llega a DURACION_TOTAL_NIVEL, y ese reloj sólo avanza
    // jugando, así que el final está como mínimo a (duración - transcurrido).
    public bool PermiteSyncIntermedio(float margenSegundos)
    {
        if (estado == EstadoJuego.Finalizado) return false;
        if (nivelActual < niveles.Length - 1) return true;
        return DURACION_TOTAL_NIVEL - tiempoTranscurridoJuego >= margenSegundos;
    }

    private IEnumerator LanzarConfetiFinal()
    {
        GameObject[] confetis = { confetiCorazonesPrefab, confetiEstrellasPrefab, confetiMariposasPrefab };
        var esperaUnSegundo = new WaitForSeconds(1f);
        for (int i = 0; i < 10; i++)
        {
            GameObject prefab = confetis[i % confetis.Length];   // alterna los tres tipos
            if (prefab != null)
            {
                GameObject confeti = Instantiate(prefab, new Vector3(puntoCentralX, 2f, puntoCentralZ), Quaternion.identity);
                Destroy(confeti, 2f);
            }
            yield return esperaUnSegundo;
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
        switch (estado)
        {
            case EstadoJuego.Jugando:
                ActualizarJugando();
                break;
            case EstadoJuego.StandByError:
                ActualizarStandBy();
                break;
            case EstadoJuego.Practicando:
                ActualizarPractica();
                break;
            // EsperandoPractica, EsperandoInicio, MostrandoSecuencia y Finalizado no consumen reloj de nivel
            // ni evalúan el timeout de tablero (la barra queda congelada).
        }
    }

    private void ActualizarJugando()
    {
        // El reloj de nivel SÓLO corre mientras se juega.
        tiempoTranscurridoJuego += Time.deltaTime;
        if (tiempoTranscurridoJuego >= DURACION_TOTAL_NIVEL)
            finDeNivelPendiente = true;   // no cortamos el tablero; se resolverá al terminarlo

        tiempoActual += Time.deltaTime;

        // Porcentaje de tablero completado
        float progreso = Mathf.Clamp01(tiempoActual / duracionTablero);
        actualizarBarraProgreso(1 - progreso);

        // Timeout real de tablero
        if (GetRTSinceBoardSpawn() > duracionTablero * 1000f)
        {
            EventLogger.Instance.BoardTimeout(indiceActual, indiceActual / 3f);
            TableroResuelto("timeout");
        }
    }

    private void ActualizarStandBy()
    {
        // El tiempo de stand-by NO consume reloj de nivel (coherente con la presentación
        // de secuencia): ni tiempoTranscurridoJuego ni el timeout de tablero avanzan aquí.
        tiempoActualstandBy += Time.deltaTime;
        if (tiempoActualstandBy <= tiempoTotalStandBy)
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
            estado = EstadoJuego.Jugando;
            TableroResuelto("error_recovered");
        }
    }
}


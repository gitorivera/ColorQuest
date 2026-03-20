using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;
//add the random library
using Random = UnityEngine.Random;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
//using System.Numerics;

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
    private float segundosTranscurridos = 0f;

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
  
    private float  DURACION_TOTAL_NIVEL= 20f; // Duración total del juego en segundos
   
    private float tiempoTranscurridoJuego = 0f; // Tiempo transcurrido desde el inicio del juego

// variables para la flexibilidad del numero de bolas por tablero
    private int numeroBolasTablero = 3; // Número de bolas a posicionar en el tablero (puede ser ajustado)
    
    bool nivel1 = true;
    bool nivel2 = true; 
    bool nivel3 = true;
    bool nivel4 = true;
    bool nivel5 = true;

    
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
        if (SecuenciaBolas[indiceActual].name == esferaNombre)
        {
            Debug.Log($"¡Correcto! Bola {esferaNombre} clickeada en el orden correcto.");
            indiceActual++;

            // Verificar si se completó el orden, exceptuando la ultima esfera
            if (indiceActual >= 3)
            {
                Debug.Log("¡Felicidades! Has clickeado todas las bolas en el orden correcto.");
                // Reiniciar o realizar alguna acción
                puntaje += 10;
                ContadorPuntos.text = $"{puntaje}";
                indiceActual = 0;
                destruirEsferas = true;
                PosicionarBolas(numeroBolasTablero);
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
           // PosicionarBolas(numeroBolasTablero);
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
        panelSecuenciaPrefab = Instantiate(panelSecuenciaPrefab, new Vector3(puntoCentralX,puntoCentralY*0.3f, puntoCentralZ), Quaternion.identity);
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
        // Aquí puedes implementar la lógica para actualizar la barra de progreso visualmente
        // Por ejemplo, podrías cambiar el color o la escala de los cuadros según el progreso
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
        CrearMalla(2);
        ////BarajarBolasPrefabs();
        PosicionarBolas(numeroBolasTablero);
        PosicionarLetreros();
        PosicionarPanelSecuencia();
        InicializarSecuenciaBolas();
        //PonerSecuencia();
        PosicionarCuadroBarraProgreso();
    }
    
    void Update()
    {
        // Tiempo total del juego
        tiempoTranscurridoJuego += Time.deltaTime;
        // Cálculo de las centésimas que dura un frame
        if (tiempoTranscurridoJuego<DURACION_TOTAL_NIVEL)
        {
            // nivel 1
            //bool nivel1 = true;
            numeroBolasTablero = 3;
            numeroCeldas =2;
            if (nivel1)
            {
                DestruirMalla();
                CrearMalla(numeroCeldas);
                destruirEsferas = true;
                BarajarBolasPrefabs();
                PonerSecuencia();
                PosicionarBolas(numeroBolasTablero);

                nivel1 = false;
            }
        }else if (tiempoTranscurridoJuego>=DURACION_TOTAL_NIVEL && tiempoTranscurridoJuego<DURACION_TOTAL_NIVEL * 2)
        {
            // nivel 2
            //bool nivel2 = true;
            numeroBolasTablero = 4;
            numeroCeldas =3;
            if (nivel2)
            {
                DestruirMalla();
                CrearMalla(numeroCeldas);
                    destruirEsferas = true;
               
                BarajarBolasPrefabs();
                PonerSecuencia();
                PosicionarBolas(numeroBolasTablero);
               
                
                nivel2 = false;
            }

        }else if (tiempoTranscurridoJuego>=DURACION_TOTAL_NIVEL * 2 && tiempoTranscurridoJuego<DURACION_TOTAL_NIVEL * 3)
        {
            // nivel 3
            //bool nivel3 = true;
            numeroBolasTablero = 5;
            numeroCeldas =4;
            if (nivel3)
            {
                DestruirMalla();
                CrearMalla(numeroCeldas);
                destruirEsferas = true;
                BarajarBolasPrefabs();
                PonerSecuencia();
                PosicionarBolas(numeroBolasTablero);
                nivel3 = false;
            }
        }else if (tiempoTranscurridoJuego>=DURACION_TOTAL_NIVEL * 3 && tiempoTranscurridoJuego<DURACION_TOTAL_NIVEL * 4)
        {
            // nivel 4
            //bool nivel4 = true;
            numeroBolasTablero = 6;
            numeroCeldas =5;
            if (nivel4)
            {
                DestruirMalla();
                CrearMalla(numeroCeldas);
                destruirEsferas = true;
                BarajarBolasPrefabs();
                PonerSecuencia();
                PosicionarBolas(numeroBolasTablero);
                nivel4 = false;
            }
        }else if (tiempoTranscurridoJuego>=DURACION_TOTAL_NIVEL * 4 && tiempoTranscurridoJuego<DURACION_TOTAL_NIVEL * 5)
        {
            // nivel 5
            
            //bool nivel5 = true;
            numeroBolasTablero = 7;
            numeroCeldas =6;
            if (nivel5)
            {
                DestruirMalla();
                CrearMalla(numeroCeldas);
                destruirEsferas = true;
                BarajarBolasPrefabs();
                PonerSecuencia();
                PosicionarBolas(numeroBolasTablero);
                nivel5 = false;
            }

        }else
        {
            tiempoTranscurridoJuego = 0f;
            nivel1 = true;
            nivel2 = true;
            nivel3 = true;
            nivel4 = true;
            nivel5 = true;
        }
        
    
        
        tiempoActual += Time.deltaTime;
     
       
         

        float progreso =Mathf.Clamp01(tiempoActual / duracionTablero);
         actualizarBarraProgreso(1-progreso);
        
        


      //  RelojContador.text = $"{segundosTranscurridos:00}:{tiempoActual:00}";
        if (tiempoActual > duracionTablero)
        {
            tiempoActual = 0;
            destruirEsferas = true;
            PosicionarBolas(numeroBolasTablero);
            indiceActual = 0;
             // Reiniciar el contador de tiempo y actualizar el reloj
        }
        

    }


}
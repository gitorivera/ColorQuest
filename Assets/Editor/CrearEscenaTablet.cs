using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Arma Assets/Scenes/TabletScene.unity a partir de DefaultScene (versión Quest):
//  - quita todo lo de Meta (Camera Rig con los blasters, Passthrough, MRUK, Effect Mesh,
//    Poke Interaction y el botón INICIAR, que es un PokeInteractable del SDK);
//  - pone un botón INICIAR propio (cubo + texto) en el plano del tablero, que se dispara
//    con un toque igual que el de la Quest se dispara con el blaster;
//  - agrega la cámara fija (EncuadreTablet) con el TouchShooter;
//  - cambia el source_id del stream TET a "juego-tablet" y los textos que hablan del
//    control y del gatillo;
//  - deja TabletScene como única escena del build.
// Se puede volver a correr cuando cambie DefaultScene: la sobrescribe desde cero. Ojo: en
// TabletBranch ya no está el SDK de Meta, así que DefaultScene aquí tiene prefabs y scripts
// faltantes y el botón de Meta no se reconoce. Para regenerar, traer la DefaultScene nueva
// y correrlo en una copia con el SDK, o revisar a mano lo que quede de Meta.
public static class CrearEscenaTablet
{
    const string RutaEscenaQuest = "Assets/Scenes/DefaultScene.unity";
    const string RutaEscenaTablet = "Assets/Scenes/TabletScene.unity";
    const string RutaPanelQuest = "Assets/prefabs/PanelInstrucciones.prefab";
    const string RutaPanelTablet = "Assets/prefabs/PanelInstruccionesTablet.prefab";
    const string RutaMatBoton = "Assets/materials/MatBotonTablet.mat";
    const string RutaRayo = "Assets/prefabs/RayoRojo.prefab";
    const string RutaImpacto = "Assets/prefabs/ImpactoRayo.prefab";
    const string RutaExplosion = "Assets/prefabs/ExplosionParticulas.prefab";
    const string RutaSonido = "Assets/prefabs/BlasterShoot.mp3";

    // Mismo plano que el tablero (GameController.puntoCentralZ), debajo del panel de instrucciones
    static readonly Vector3 PosicionBoton = new Vector3(0f, 1.0f, 1.5f);
    static readonly Vector3 TamanoBoton = new Vector3(0.32f, 0.11f, 0.03f);
    const int CapaIgnorarRaycast = 2;

    static readonly string[] RaicesMeta =
    {
        "[BuildingBlock] Camera Rig",
        "[BuildingBlock] Passthrough",
        "[BuildingBlock] MR Utility Kit",
        "[BuildingBlock] Effect Mesh",
        "Poke Interaction",
    };

    const string TextoBienvenidaTablet =
        "<align=center><b><size=125%>¡Bienvenido a ColorQuest!</size></b>\n" +
        "Un juego de puntería y memoria. ¡Esperamos que lo disfrutes!</align>\n" +
        "Antes de empezar vamos a <b>practicar la puntería durante {segundos} segundos</b>.\n" +
        "<b>Toca</b> cada esfera que aparezca en la cuadrícula. La práctica no suma puntos.\n" +
        "<align=center><b>Cuando estés listo, toca el botón \"Iniciar práctica\".</b></align>";

    // Reemplazos sobre el texto del panel de instrucciones de la Quest
    static readonly (string quest, string tablet)[] ReemplazosInstrucciones =
    {
        ("2. Dispara a las esferas del tablero", "2. Toca las esferas del tablero"),
        ("el control <b>vibra</b>", "la tablet <b>vibra</b>"),
        ("Dispara a Iniciar cuando estés listo.", "Toca Iniciar cuando estés listo."),
    };

    [MenuItem("ColorQuest/Crear escena tablet")]
    public static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        GameObject panelTablet = CrearPanelTablet();
        Material matBoton = CrearMaterialBoton();

        var escenaQuest = EditorSceneManager.OpenScene(RutaEscenaQuest, OpenSceneMode.Single);
        if (!EditorSceneManager.SaveScene(escenaQuest, RutaEscenaTablet, true))
        {
            Debug.LogError($"[CrearEscenaTablet] No se pudo copiar {RutaEscenaQuest} a {RutaEscenaTablet}");
            return;
        }
        var escena = EditorSceneManager.OpenScene(RutaEscenaTablet, OpenSceneMode.Single);

        GameController gc = Object.FindFirstObjectByType<GameController>(FindObjectsInactive.Include);
        if (gc == null)
        {
            Debug.LogError("[CrearEscenaTablet] La escena no tiene GameController");
            return;
        }
        var soGc = new SerializedObject(gc);

        // 1. Botón INICIAR de Meta (PokeInteractable) y raíces del SDK
        var quitados = new List<string>();
        var botonQuest = soGc.FindProperty("botonInicio").objectReferenceValue as GameObject;
        if (botonQuest != null)
        {
            GameObject raizBoton = PrefabUtility.GetOutermostPrefabInstanceRoot(botonQuest) ?? botonQuest.transform.root.gameObject;
            quitados.Add(raizBoton.name);
            Object.DestroyImmediate(raizBoton);
        }
        foreach (GameObject raiz in escena.GetRootGameObjects())
        {
            if (raiz != null && RaicesMeta.Contains(raiz.name))
            {
                quitados.Add(raiz.name);
                Object.DestroyImmediate(raiz);
            }
        }
        int componentesMeta = QuitarComponentesMeta(escena);

        // 2. Botón INICIAR propio
        GameObject boton = CrearBoton(matBoton);
        soGc.FindProperty("botonInicio").objectReferenceValue = boton;
        soGc.FindProperty("textoBienvenida").stringValue = TextoBienvenidaTablet;
        if (panelTablet != null) soGc.FindProperty("panelInstruccionesPrefab").objectReferenceValue = panelTablet;
        soGc.ApplyModifiedPropertiesWithoutUndo();

        // 3. Cámara fija con disparo táctil
        CrearCamara();

        // 4. LSL: mismo stream y mismos códigos, otro source_id para distinguir la versión
        LSLMarkerSender lsl = Object.FindFirstObjectByType<LSLMarkerSender>(FindObjectsInactive.Include);
        if (lsl != null)
        {
            var soLsl = new SerializedObject(lsl);
            soLsl.FindProperty("tetSourceId").stringValue = "juego-tablet";
            soLsl.ApplyModifiedPropertiesWithoutUndo();
        }
        else Debug.LogWarning("[CrearEscenaTablet] No hay LSLMarkerSender en la escena");

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);

        // 5. Única escena del build en esta rama
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(RutaEscenaTablet, true) };

        Debug.Log($"[CrearEscenaTablet] {RutaEscenaTablet} lista. Quitados: {string.Join(", ", quitados)}; " +
                  $"{componentesMeta} componentes de Meta sueltos.");
    }

    static GameObject CrearBoton(Material mat)
    {
        GameObject boton = GameObject.CreatePrimitive(PrimitiveType.Cube);
        boton.name = "BotonInicioTablet";
        boton.transform.SetPositionAndRotation(PosicionBoton, Quaternion.identity);
        boton.transform.localScale = TamanoBoton;
        boton.layer = 0;   // Default: está en la capaImpacto del TouchShooter
        if (mat != null) boton.GetComponent<MeshRenderer>().sharedMaterial = mat;

        // Texto como hijo sin escala del cubo, para que no se deforme
        var texto = new GameObject("Etiqueta", typeof(TextMeshPro));
        texto.layer = CapaIgnorarRaycast;
        texto.transform.SetParent(boton.transform, false);
        texto.transform.localScale = new Vector3(1f / TamanoBoton.x, 1f / TamanoBoton.y, 1f / TamanoBoton.z);
        texto.transform.localPosition = new Vector3(0f, 0f, -0.51f);   // justo delante de la cara frontal
        var tmp = texto.GetComponent<TextMeshPro>();
        tmp.rectTransform.sizeDelta = new Vector2(TamanoBoton.x * 0.9f, TamanoBoton.y * 0.8f);
        tmp.text = "Iniciar";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.fontStyle = FontStyles.Bold;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 0.05f;
        tmp.fontSizeMax = 0.6f;
        // GameController lo desactiva en Start si hay práctica; arranca activo como el de la Quest
        boton.SetActive(true);
        return boton;
    }

    static void CrearCamara()
    {
        var go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        var cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.16f, 0.18f, 0.22f);
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 50f;
        go.AddComponent<AudioListener>();
        go.AddComponent<EncuadreTablet>();

        var audio = go.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;

        var disparo = go.AddComponent<TouchShooter>();
        var so = new SerializedObject(disparo);
        so.FindProperty("camara").objectReferenceValue = cam;
        so.FindProperty("audioSource").objectReferenceValue = audio;
        so.FindProperty("sonidoDisparo").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(RutaSonido);
        so.FindProperty("efectos.rayoPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<LineRenderer>(RutaRayo);
        so.FindProperty("efectos.impactoRayoPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(RutaImpacto);
        so.FindProperty("efectos.explosionParticulasPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(RutaExplosion);
        so.FindProperty("efectos.alcance").floatValue = 5f;          // fuerzaDisparo del Blaster
        so.FindProperty("efectos.tiempoVidaRayo").floatValue = 0.3f; // tiempoVidaRayo del Blaster
        so.FindProperty("capaImpacto").intValue = 1;                 // Default, como el Blaster
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Componentes del SDK de Meta que hayan quedado en otros objetos, y scripts faltantes.
    static int QuitarComponentesMeta(UnityEngine.SceneManagement.Scene escena)
    {
        int total = 0;
        foreach (GameObject raiz in escena.GetRootGameObjects())
        {
            foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
            {
                total += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                // Varias pasadas: un componente puede requerir otro (RequireComponent)
                for (int pasada = 0; pasada < 3; pasada++)
                {
                    foreach (MonoBehaviour mb in t.GetComponents<MonoBehaviour>())
                    {
                        if (mb == null || !EsDeMeta(mb.GetType())) continue;
                        Object.DestroyImmediate(mb);
                        total++;
                    }
                }
            }
        }
        return total;
    }

    static bool EsDeMeta(System.Type tipo)
    {
        string ns = tipo.Namespace ?? "";
        return ns.StartsWith("Oculus") || ns.StartsWith("Meta") || tipo.Name.StartsWith("OVR");
    }

    static Material CrearMaterialBoton()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(RutaMatBoton);
        if (mat != null) return mat;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        mat = new Material(shader);
        Color verde = new Color(0.13f, 0.55f, 0.33f);
        mat.SetColor("_BaseColor", verde);
        mat.SetColor("_Color", verde);
        AssetDatabase.CreateAsset(mat, RutaMatBoton);
        return mat;
    }

    // Copia del panel de instrucciones con el texto adaptado al toque
    static GameObject CrearPanelTablet()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(RutaPanelQuest) == null)
        {
            Debug.LogWarning($"[CrearEscenaTablet] No está {RutaPanelQuest}; se deja el panel de la Quest");
            return null;
        }
        GameObject contenido = PrefabUtility.LoadPrefabContents(RutaPanelQuest);
        try
        {
            TMP_Text texto = contenido.GetComponentInChildren<TMP_Text>(true);
            if (texto != null)
            {
                string t = texto.text;
                foreach (var (quest, tablet) in ReemplazosInstrucciones)
                {
                    if (!t.Contains(quest)) Debug.LogWarning($"[CrearEscenaTablet] El panel ya no dice «{quest}»: revisar el texto de la tablet");
                    t = t.Replace(quest, tablet);
                }
                texto.text = t;
            }
            return PrefabUtility.SaveAsPrefabAsset(contenido, RutaPanelTablet);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contenido);
        }
    }
}

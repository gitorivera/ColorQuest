using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Arma Assets/prefabs/PanelInstrucciones.prefab a partir del tablero modelado en Blender
// (Assets/Models/PanelInstrucciones.fbx, fuente en Blender/PanelInstrucciones.blend) y le
// agrega el texto de instrucciones con TextMeshPro. Después lo asigna al GameController de
// la escena. El texto se puede editar luego directamente en el prefab.
public static class CrearPanelInstrucciones
{
    const string RutaModelo = "Assets/Models/PanelInstrucciones.fbx";
    const string RutaPrefab = "Assets/prefabs/PanelInstrucciones.prefab";
    const string RutaMatSecuencia = "Assets/materials/MatPanelSecuencia.mat";
    const string RutaMatFondo = "Assets/materials/MatPanelInstruccionesFondo.mat";
    const string RutaMatMarco = "Assets/materials/MatPanelInstruccionesMarco.mat";
    const string RutaEscena = "Assets/Scenes/DefaultScene.unity";

    const float AnchoTablero = 0.9f;                     // m, igual que en Blender
    static readonly Vector2 AreaTexto = new Vector2(0.80f, 0.42f);
    const int CapaIgnorarRaycast = 2;

    const string Texto =
        "<align=center><b><size=125%>¿Cómo se juega?</size></b></align>\n" +
        "1. Al empezar cada nivel verás <b>3 esferas de colores</b>: esa es tu secuencia. ¡Memorízala!\n" +
        "2. Dispara a las esferas del tablero <b>en ese mismo orden</b>.\n" +
        "3. Si te equivocas, el tablero se pone <b>rojo</b> y el control <b>vibra</b>. Espera un momento y sigue jugando.\n" +
        "4. Los <b>cuadros de arriba</b> muestran el tiempo que queda en cada tablero.\n" +
        "Cada secuencia completa suma <b>10 puntos</b>. Hay 5 niveles y el tablero crece en cada uno.\n" +
        "<align=center><b>Dispara a Iniciar cuando estés listo.</b></align>";

    [MenuItem("ColorQuest/Crear panel de instrucciones")]
    public static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        AssetDatabase.Refresh();

        var mi = (ModelImporter)AssetImporter.GetAtPath(RutaModelo);
        if (mi == null)
        {
            Debug.LogError($"[CrearPanelInstrucciones] No se encontró {RutaModelo}");
            return;
        }
        mi.materialImportMode = ModelImporterMaterialImportMode.None; // usamos materiales URP propios
        mi.importCameras = false;
        mi.importLights = false;
        mi.addCollider = false;
        mi.SaveAndReimport();

        // 1. Materiales: el fondo copia el azul translúcido del panel de la secuencia
        var matFondo = AssetDatabase.LoadAssetAtPath<Material>(RutaMatFondo);
        if (matFondo == null)
        {
            var matSecuencia = AssetDatabase.LoadAssetAtPath<Material>(RutaMatSecuencia);
            matFondo = new Material(matSecuencia);
            AssetDatabase.CreateAsset(matFondo, RutaMatFondo);
        }
        Color azul = new Color(0.5725f, 0.7843f, 1f, 0.92f);   // algo más opaco que la secuencia, para leer
        matFondo.SetColor("_BaseColor", azul);
        matFondo.SetColor("_Color", azul);
        EditorUtility.SetDirty(matFondo);

        var matMarco = AssetDatabase.LoadAssetAtPath<Material>(RutaMatMarco);
        if (matMarco == null)
        {
            matMarco = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(matMarco, RutaMatMarco);
        }
        matMarco.SetColor("_BaseColor", new Color(0.95f, 0.97f, 1f, 1f));
        matMarco.SetFloat("_Smoothness", 0.5f);
        EditorUtility.SetDirty(matMarco);

        // 2. Tablero
        var raiz = new GameObject("PanelInstrucciones");
        var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(RutaModelo);
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(modelo);
        visual.transform.SetParent(raiz.transform, false);

        var mf = visual.GetComponentInChildren<MeshFilter>();
        var rend = visual.GetComponentInChildren<MeshRenderer>();
        Mesh mesh = mf.sharedMesh;

        // Orientación: el tablero debe quedar en el plano XY (frente hacia -Z, hacia el jugador).
        // Si la importación lo dejó acostado (grosor en Y), se gira 90° en X.
        Bounds b = rend.bounds;
        if (b.size.y < b.size.z) visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f) * visual.transform.localRotation;
        // Escala: forzamos el ancho real del modelo aunque cambien las unidades del FBX
        b = rend.bounds;
        visual.transform.localScale *= AnchoTablero / b.size.x;
        b = rend.bounds;
        visual.transform.localPosition -= b.center;   // centrado en la raíz

        // Submallas: la del fondo es la más delgada (8 mm frente a 16 mm del marco)
        int ejeDelgado = mesh.bounds.size.x < mesh.bounds.size.y
            ? (mesh.bounds.size.x < mesh.bounds.size.z ? 0 : 2)
            : (mesh.bounds.size.y < mesh.bounds.size.z ? 1 : 2);
        var mats = new Material[mesh.subMeshCount];
        for (int i = 0; i < mats.Length; i++) mats[i] = matMarco;
        if (mesh.subMeshCount == 2)
        {
            float g0 = mesh.GetSubMesh(0).bounds.size[ejeDelgado];
            float g1 = mesh.GetSubMesh(1).bounds.size[ejeDelgado];
            mats[g0 < g1 ? 0 : 1] = matFondo;
        }
        else
        {
            Debug.LogWarning($"[CrearPanelInstrucciones] Se esperaban 2 submallas y hay {mesh.subMeshCount}; todo queda con el material del marco.");
        }
        rend.sharedMaterials = mats;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // 3. Texto (por delante del fondo y dentro del marco)
        var goTexto = new GameObject("Texto");
        goTexto.transform.SetParent(raiz.transform, false);
        var tmp = goTexto.AddComponent<TextMeshPro>();
        tmp.font = TMP_Settings.defaultFontAsset;     // LiberationSans: trae tildes, ¿ y ¡
        tmp.text = Texto;
        tmp.color = new Color32(18, 48, 90, 255);
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 0.1f;
        tmp.fontSizeMax = 0.6f;
        tmp.paragraphSpacing = 6f;
        tmp.rectTransform.sizeDelta = AreaTexto;
        goTexto.transform.localPosition = new Vector3(0f, 0f, -0.006f);
        goTexto.GetComponent<MeshRenderer>().sortingOrder = 1;   // siempre por encima del fondo translúcido

        // 4. El panel no debe interceptar el láser ni los disparos
        foreach (var c in raiz.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        foreach (var t in raiz.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = CapaIgnorarRaycast;

        var prefab = PrefabUtility.SaveAsPrefabAsset(raiz, RutaPrefab);
        Object.DestroyImmediate(raiz);
        AssetDatabase.SaveAssets();
        Debug.Log($"[CrearPanelInstrucciones] {RutaPrefab} creado (tamaño {b.size}).");

        // 5. Asignarlo al GameController de la escena
        EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Single);
        var gc = Object.FindFirstObjectByType<GameController>(FindObjectsInactive.Include);
        if (gc == null)
        {
            Debug.LogWarning("[CrearPanelInstrucciones] No se encontró GameController en la escena; asigna el prefab a mano.");
            return;
        }
        var so = new SerializedObject(gc);
        so.FindProperty("panelInstruccionesPrefab").objectReferenceValue = prefab;
        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(gc.gameObject.scene);
        EditorSceneManager.SaveScene(gc.gameObject.scene);
        Debug.Log("[CrearPanelInstrucciones] Listo: prefab asignado a GameController.panelInstruccionesPrefab.");
    }
}

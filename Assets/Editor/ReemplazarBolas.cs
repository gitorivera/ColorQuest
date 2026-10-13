using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Reemplaza Assets/prefabs/Bola1..Bola15.prefab por las esferas infantiles de Assets/Models/Esferas.
// Conserva el nombre de archivo y el nombre del GameObject raiz ("Bola1".."Bola10"), que el juego usa
// como ID de cada bola (GameController / marcadores LSL), y el guid de cada .prefab.
public static class ReemplazarBolas
{
    const string DirModelos = "Assets/Models/Esferas";
    const string DirPrefabs = "Assets/prefabs";
    const string DirMateriales = "Assets/materials";
    const string RutaEscena = "Assets/Scenes/DefaultScene.unity";

    static readonly string[] Nombres =
    {
        "01_Estrella", "02_Corazon", "03_CaritaFeliz", "04_Sandia", "05_LunaEstrellas",
        "06_Pez", "07_Flor", "08_Monstruo", "09_Arcoiris", "10_Panda",
        "11_Helado", "12_Mariposa", "13_Cohete", "14_Gatito", "15_Rana",
    };

    // Orienta el modelo para que el motivo principal mire al jugador (-Z) y quede derecho.
    // El vertice con UV (0.5, 0.5) es el centro del motivo: se mide sobre el mesh importado y se
    // gira hacia -Z. Despues se aplica un giro de -90 grados en Z (sentido horario visto por el
    // jugador); sin el, el dibujo se ve girado 90 grados antihorario (verificado con un render
    // de las 15 bolas desde la posicion del jugador).
    static Quaternion OrientacionHaciaJugador(GameObject visual)
    {
        var mf = visual.GetComponentInChildren<MeshFilter>();
        Mesh mesh = mf.sharedMesh;
        Vector2[] uv = mesh.uv;
        Vector3[] v = mesh.vertices;
        int iFrente = 0;
        float mejor = float.MaxValue;
        for (int k = 0; k < uv.Length; k++)
        {
            float d = (uv[k] - new Vector2(0.5f, 0.5f)).sqrMagnitude;
            if (d < mejor) { mejor = d; iFrente = k; }
        }
        Transform padre = visual.transform.parent;
        Vector3 frente = padre.InverseTransformDirection(mf.transform.TransformDirection(v[iFrente])).normalized;
        return Quaternion.Euler(0f, 0f, -90f) * Quaternion.FromToRotation(frente, Vector3.back);
    }

    [MenuItem("ColorQuest/Reemplazar Bola1-15 por esferas infantiles")]
    public static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        AssetDatabase.Refresh();

        // 1. Apuntar la escena y recordar que prefab usa cada posicion de bolasPrefabs (antes de sobrescribir).
        EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Single);
        var gc = Object.FindFirstObjectByType<GameController>(FindObjectsInactive.Include);
        SerializedObject so = gc != null ? new SerializedObject(gc) : null;
        SerializedProperty arr = so?.FindProperty("bolasPrefabs");
        var rutasEscena = new List<string>();
        if (arr != null)
            for (int k = 0; k < arr.arraySize; k++)
                rutasEscena.Add(AssetDatabase.GetAssetPath(arr.GetArrayElementAtIndex(k).objectReferenceValue));
        else
            Debug.LogWarning("[ReemplazarBolas] No se encontro GameController.bolasPrefabs en la escena; solo se reescriben los prefabs.");

        // 2. Reconstruir cada prefab.
        var reemplazados = new HashSet<string>();
        for (int i = 1; i <= Nombres.Length; i++)
        {
            string n = Nombres[i - 1];
            string fbx = $"{DirModelos}/Esfera_{n}.fbx";
            string png = $"{DirModelos}/Textures/Esfera_{n}.png";

            var mi = (ModelImporter)AssetImporter.GetAtPath(fbx);
            mi.materialImportMode = ModelImporterMaterialImportMode.None; // usamos nuestro propio material URP
            mi.importCameras = false;
            mi.importLights = false;
            mi.SaveAndReimport();

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(png);
            string rutaMat = $"{DirMateriales}/MaterialEsfera_{n}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(rutaMat);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, rutaMat);
            }
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.65f);
            EditorUtility.SetDirty(mat);

            var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            var raiz = new GameObject($"Bola{i}");                  // el nombre es el ID de la bola
            raiz.transform.localScale = Vector3.one * 0.1f;         // misma escala que los prefabs anteriores
            raiz.AddComponent<SphereCollider>().radius = 0.5f;      // mismo collider que los prefabs anteriores

            var visual = (GameObject)PrefabUtility.InstantiatePrefab(modelo);
            visual.transform.SetParent(raiz.transform, false);
            visual.transform.localRotation = OrientacionHaciaJugador(visual);
            foreach (var r in visual.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;

            string rutaPrefab = $"{DirPrefabs}/Bola{i}.prefab";
            PrefabUtility.SaveAsPrefabAsset(raiz, rutaPrefab);     // sobrescribe y conserva el guid del .prefab
            Object.DestroyImmediate(raiz);
            reemplazados.Add(rutaPrefab);
            Debug.Log($"[ReemplazarBolas] {rutaPrefab} <- {n}");
        }
        AssetDatabase.SaveAssets();

        // 3. Volver a enlazar la escena (cambian los fileID internos de la raiz de cada prefab).
        if (arr != null)
        {
            for (int k = 0; k < rutasEscena.Count; k++)
                if (reemplazados.Contains(rutasEscena[k]))
                    arr.GetArrayElementAtIndex(k).objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(rutasEscena[k]);
            so.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(gc.gameObject.scene);
            EditorSceneManager.SaveScene(gc.gameObject.scene);
        }
        AssetDatabase.Refresh();
        Debug.Log("[ReemplazarBolas] Listo: Bola1..Bola15 reemplazadas.");
    }
}

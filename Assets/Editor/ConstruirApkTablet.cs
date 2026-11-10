using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Construye el APK de la versión tablet con las escenas del build (TabletScene) en
// build/tablet/ColorQuestTablet.apk. También sirve en batch:
//   Unity.exe -batchmode -quit -projectPath . -buildTarget Android -executeMethod ConstruirApkTablet.Run
public static class ConstruirApkTablet
{
    const string Salida = "build/tablet/ColorQuestTablet.apk";

    [MenuItem("ColorQuest/Construir APK tablet")]
    public static void Run()
    {
        string[] escenas = EditorBuildSettings.scenes.Where(e => e.enabled).Select(e => e.path).ToArray();
        if (escenas.Length == 0)
        {
            Debug.LogError("[ConstruirApkTablet] No hay escenas en el build (correr ColorQuest > Crear escena tablet)");
            return;
        }

        EditorUserBuildSettings.buildAppBundle = false;
        var opciones = new BuildPlayerOptions
        {
            scenes = escenas,
            locationPathName = Salida,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };
        BuildReport reporte = BuildPipeline.BuildPlayer(opciones);
        BuildSummary resumen = reporte.summary;
        Debug.Log($"[ConstruirApkTablet] {resumen.result}: {Salida}, {resumen.totalSize / (1024 * 1024)} MB, " +
                  $"{resumen.totalErrors} errores, {resumen.totalTime.TotalSeconds:0} s");
        if (Application.isBatchMode && resumen.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}

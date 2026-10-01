using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class Build
{
    // Unity.exe -batchmode -quit -projectPath . -executeMethod Build.Windows
    public static void Windows()
    {
        var report = BuildPipeline.BuildPlayer(new[] { "Assets/Scenes/SampleScene.unity" }, "Build/Bastiao.exe",
            BuildTarget.StandaloneWindows64, BuildOptions.None);
        Debug.Log($"Build: {report.summary.result} ({report.summary.totalErrors} erros, {report.summary.totalSize / (1024 * 1024)} MB)");
        if (report.summary.result != BuildResult.Succeeded && Application.isBatchMode) EditorApplication.Exit(1);
    }
}

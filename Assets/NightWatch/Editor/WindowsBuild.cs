using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Night Watch > Build Windows: makes the hand-in Windows build (NightWatch.exe and its folder)
// with only our scene. It goes to D:\NightWatchBuild (the C: drive is short of space), or to a
// Build folder next to the project if there is no D: drive. Played with keyboard and mouse
// through the XR Interaction Simulator (it is set to start in builds too).
public static class WindowsBuild
{
    const string Scene = "Assets/Scenes/NightWatch.unity";

    static string Folder => Directory.Exists("D:/") ? "D:/NightWatchBuild" : Path.GetFullPath("Build");

    [MenuItem("Night Watch/Build Windows")]
    static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Night Watch", "Stop Play mode first, then build.", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("Night Watch",
                $"Build the Windows game into {Folder}?\nThis takes several minutes. Save the scene first.", "Build", "Cancel"))
            return;

        // Controller types and company name (also set by Build Full Game).
        StartupSetup.ProjectSettings();

        var options = new BuildPlayerOptions
        {
            scenes = new[] { Scene },
            locationPathName = Path.Combine(Folder, "NightWatch.exe"),
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None,
        };
        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;
        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"Night Watch: Windows build done in {summary.totalTime.TotalMinutes:0.0} min, " +
                      $"{summary.totalSize / (1024f * 1024f):0} MB, at {options.locationPathName}");
            EditorUtility.RevealInFinder(options.locationPathName);
        }
        else
        {
            Debug.LogError($"Night Watch: Windows build {summary.result} with {summary.totalErrors} errors. See the Console.");
        }
    }
}

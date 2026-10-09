#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Builds only the old PUN-based VCMI 17 WebGL player for the compatibility test.
/// The output layout intentionally matches the separate test page:
/// Builds/Web/Build/Web.loader.js and its companion files.
/// </summary>
public static class VCMI17CompatibilityBuild
{
    public static void Build()
    {
        var scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (scenes.Length == 0)
            throw new InvalidOperationException("No enabled scenes were found in EditorBuildSettings.");

        if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            throw new InvalidOperationException("Unity could not switch the project to the WebGL build target.");

        var projectRoot = Directory.GetParent(Application.dataPath).FullName;
        var outputPath = Path.Combine(projectRoot, "Builds", "Web");

        if (Directory.Exists(outputPath))
            Directory.Delete(outputPath, true);

        Directory.CreateDirectory(outputPath);

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new InvalidOperationException(
                "VCMI 17 WebGL build failed: " + report.summary.result +
                " (" + report.summary.totalErrors + " errors). Check the Unity build log.");
        }

        var loaderPath = Path.Combine(outputPath, "Build", "Web.loader.js");
        if (!File.Exists(loaderPath))
            throw new FileNotFoundException("Unity reported success, but the expected Web.loader.js was not generated.", loaderPath);

        Debug.Log("VCMI 17.0.11 WebGL compatibility build succeeded. Output: " + outputPath);
        Debug.Log("Build size: " + report.summary.totalSize + " bytes.");
    }
}
#endif

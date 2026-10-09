#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Builds only the older PUN-based VCMI 17 WebGL player for the isolated compatibility test.
/// Standardizes Unity's generated WebGL file names so the test index uses Web.* filenames,
/// without changing the original game project or the VCMI v20 build.
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

        var buildFilesPath = Path.Combine(outputPath, "Build");
        if (!Directory.Exists(buildFilesPath))
        {
            // Some Unity templates may place the generated files directly in the output folder.
            buildFilesPath = outputPath;
        }

        NormalizeAsset(buildFilesPath, @"\.loader\.js$", "Web.loader.js", required: true);
        NormalizeAsset(buildFilesPath, @"\.data(?:\.[A-Za-z0-9_-]+)?$", "Web.data.unityweb", required: true);
        NormalizeAsset(buildFilesPath, @"\.framework\.js(?:\.[A-Za-z0-9_-]+)?$", "Web.framework.js.unityweb", required: true);
        NormalizeAsset(buildFilesPath, @"\.wasm(?:\.[A-Za-z0-9_-]+)?$", "Web.wasm.unityweb", required: true);

        foreach (var expected in new[]
        {
            "Web.loader.js",
            "Web.data.unityweb",
            "Web.framework.js.unityweb",
            "Web.wasm.unityweb"
        })
        {
            if (!File.Exists(Path.Combine(buildFilesPath, expected)))
                throw new FileNotFoundException(
                    "The WebGL build is missing expected output '" + expected +
                    "'. Check the project's WebGL compression settings.", Path.Combine(buildFilesPath, expected));
        }

        Debug.Log("VCMI 17.0.11 WebGL compatibility build succeeded. Output: " + outputPath);
        Debug.Log("Build size: " + report.summary.totalSize + " bytes.");
    }

    private static void NormalizeAsset(string folder, string tailPattern, string expectedName, bool required)
    {
        var expectedPath = Path.Combine(folder, expectedName);
        if (File.Exists(expectedPath))
            return;

        var candidates = Directory.GetFiles(folder, "*", SearchOption.TopDirectoryOnly)
            .Where(path => Regex.IsMatch(Path.GetFileName(path), tailPattern, RegexOptions.IgnoreCase))
            .ToArray();

        if (candidates.Length == 0)
        {
            if (required)
                throw new FileNotFoundException("Unity did not generate the expected asset pattern " + tailPattern, folder);
            return;
        }

        if (candidates.Length > 1)
            throw new InvalidOperationException("Found multiple files matching " + tailPattern + ": " + string.Join(", ", candidates));

        var sourcePath = candidates[0];
        var match = Regex.Match(Path.GetFileName(sourcePath), tailPattern, RegexOptions.IgnoreCase);
        var destinationPath = Path.Combine(folder, expectedName);

        if (File.Exists(destinationPath))
            File.Delete(destinationPath);
        if (String.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase))
            return;

        // Keep WebGL's compression-fallback extension so our loader can fetch the file normally.
        var suffix = match.Value;
        var chosenName = "Web" + suffix;
        destinationPath = Path.Combine(folder, chosenName);
        if (File.Exists(destinationPath))
            File.Delete(destinationPath);
        File.Move(sourcePath, destinationPath);
    }
}
#endif

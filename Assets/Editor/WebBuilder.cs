using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Headless Web build entry point.
/// <code>
/// Unity -batchmode -quit -nographics -projectPath . -buildTarget WebGL \
///       -executeMethod WebBuilder.Build -buildOutput &lt;path&gt;
/// </code>
/// </summary>
public static class WebBuilder
{
    private const string DefaultOutputPath = "Build/Web";

    public static void Build()
    {
        string outputPath = ReadArgument("-buildOutput") ?? DefaultOutputPath;

        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            Fail("No enabled scenes in the build settings.");
            return;
        }

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.WebGL,
            targetGroup = BuildTargetGroup.WebGL,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        Debug.Log($"[WebBuilder] {summary.result} — {summary.totalSize / 1024 / 1024} MB in {summary.totalTime}");

        if (summary.result != BuildResult.Succeeded)
        {
            Fail($"Build {summary.result} with {summary.totalErrors} error(s).");
            return;
        }

        EditorApplication.Exit(0);
    }

    private static string ReadArgument(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, name);
        return index >= 0 && index < args.Length - 1 ? args[index + 1] : null;
    }

    private static void Fail(string message)
    {
        Debug.LogError($"[WebBuilder] {message}");
        EditorApplication.Exit(1);
    }
}

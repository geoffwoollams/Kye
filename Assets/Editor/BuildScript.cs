using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Command line build entry point, used by build.sh:
///   Unity -batchmode -quit -buildTarget Linux64 -executeMethod BuildScript.Build -exportPath Export/Linux -apkName Kye-1.2.3
/// Builds the active build target (set by -buildTarget) into -exportPath.
/// </summary>
public static class BuildScript
{
    const string ProductFileName = "Kye";

    public static void Build()
    {
        string exportPath = GetArg("-exportPath");
        if (string.IsNullOrEmpty(exportPath))
            Fail("Missing -exportPath");

        var target = EditorUserBuildSettings.activeBuildTarget;
        string location;
        switch (target)
        {
            case BuildTarget.StandaloneLinux64:
                location = Path.Combine(exportPath, ProductFileName + ".x86_64");
                break;
            case BuildTarget.StandaloneWindows64:
                location = Path.Combine(exportPath, ProductFileName + ".exe");
                break;
            case BuildTarget.StandaloneOSX:
                location = Path.Combine(exportPath, ProductFileName + ".app");
                break;
            case BuildTarget.Android:
                EditorUserBuildSettings.buildAppBundle = false; // we want an .apk, not an .aab
                location = Path.Combine(exportPath, (GetArg("-apkName") ?? ProductFileName) + ".apk");
                break;
            case BuildTarget.WebGL:
                location = exportPath;
                break;
            default:
                Fail("Unsupported build target " + target);
                return;
        }

        Directory.CreateDirectory(exportPath);

        var options = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = location,
            target = target,
            options = BuildOptions.None,
        };

        // Windows and Mac build with CoreCLR; IL2CPP can't build them. Linux, Android and WebGL keep the project setting.
        // Windows, Mac and Linux share the Standalone group, so override it for this run only and restore it afterwards.
        bool useCoreClr = target == BuildTarget.StandaloneWindows64 || target == BuildTarget.StandaloneOSX;
        var buildTarget = NamedBuildTarget.Standalone;
        var previousBackend = PlayerSettings.GetScriptingBackend(buildTarget);
        if (useCoreClr)
            PlayerSettings.SetScriptingBackend(buildTarget, ScriptingImplementation.CoreCLR);

        BuildReport report;
        try
        {
            report = BuildPipeline.BuildPlayer(options);
        }
        finally
        {
            if (useCoreClr)
                PlayerSettings.SetScriptingBackend(buildTarget, previousBackend);
        }

        if (report.summary.result != BuildResult.Succeeded)
            Fail($"Build of {target} finished with result {report.summary.result} ({report.summary.totalErrors} errors)");

        Debug.Log($"Build of {target} succeeded: {location}");
        EditorApplication.Exit(0);
    }

    static string GetArg(string name)
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return null;
    }

    static void Fail(string message)
    {
        Debug.LogError(message);
        EditorApplication.Exit(1);
    }
}

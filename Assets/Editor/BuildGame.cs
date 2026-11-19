using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Builds the game.
///
///   Tools ▸ Prdivý závod ▸ Build player        (this computer; output: ./Builds/PrdivyZavod)
///   Tools ▸ Prdivý závod ▸ Build Android APK   (output: ./Builds/Android/PrdivyZavod.apk)
///   Tools ▸ Prdivý závod ▸ Build Android APK for old phones   (output: ./Builds/Android/PrdivyZavod-old.apk)
///
///   or headless:
///   Unity -batchmode -projectPath . -buildTarget StandaloneLinux64 -executeMethod BuildGame.Build [-buildDir DIR] -quit
///   Unity -batchmode -projectPath . -buildTarget Android -executeMethod BuildGame.BuildAndroid [-buildDir DIR] -quit
///   Unity -batchmode -projectPath . -buildTarget Android -executeMethod BuildGame.BuildAndroidOld [-buildDir DIR] -quit
///
/// The APK needs the editor's "Android Build Support" module (with its SDK, NDK
/// and OpenJDK). It is signed with the debug key, so it can be copied to a
/// phone and installed there, but not published to a store.
/// </summary>
public static class BuildGame
{
    [MenuItem("Tools/Prdivý závod/Build player")]
    public static void Build()
    {
        string dir = "Builds/PrdivyZavod";
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-buildDir") dir = args[i + 1];

        BuildTarget target;
        string exe;
        switch (Application.platform)
        {
            case RuntimePlatform.WindowsEditor: target = BuildTarget.StandaloneWindows64; exe = "PrdivyZavod.exe"; break;
            case RuntimePlatform.OSXEditor: target = BuildTarget.StandaloneOSX; exe = "PrdivyZavod.app"; break;
            default: target = BuildTarget.StandaloneLinux64; exe = "PrdivyZavod.x86_64"; break;
        }

        bool ok = Run(Path.Combine(dir, exe), target);
        if (!ok && Application.isBatchMode) EditorApplication.Exit(3);
    }

    [MenuItem("Tools/Prdivý závod/Build Android APK")]
    public static void BuildAndroid() => AndroidBuild(old: false);

    /// <summary>
    /// The same game for old or weak Android devices: also runs on 32-bit ARM,
    /// accepts the oldest Android this Unity version supports, and starts in the
    /// lightest graphics level (see GraphicsSetup).
    /// </summary>
    [MenuItem("Tools/Prdivý závod/Build Android APK for old phones")]
    public static void BuildAndroidOld() => AndroidBuild(old: true);

    static void AndroidBuild(bool old)
    {
        string dir = "Builds/Android";
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-buildDir") dir = args[i + 1];

        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
        {
            Debug.LogError("[Setup] Android Build Support is not installed for this editor (Unity Hub ▸ Installs ▸ Add modules)");
            if (Application.isBatchMode) EditorApplication.Exit(4);
            return;
        }

        // IL2CPP for 64-bit ARM is what every current phone runs; old ones also need 32-bit
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = old
            ? AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64
            : AndroidArchitecture.ARM64;
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.prdivyzavod.hra");

        // the oldest Android accepted: the project's usual floor, or for old phones the
        // lowest one this Unity version can build for
        var normalMin = AndroidSdkVersions.AndroidApiLevel25;
        var lowest = normalMin;
        foreach (AndroidSdkVersions v in Enum.GetValues(typeof(AndroidSdkVersions)))
            if ((int)v > 0 && (int)v < (int)lowest) lowest = v;
        PlayerSettings.Android.minSdkVersion = old ? lowest : normalMin;

        // landscape only, either way up
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

        PlayerSettings.Android.useCustomKeystore = false;   // debug-signed
        EditorUserBuildSettings.buildAppBundle = false;     // an .apk, not an .aab
        AssetDatabase.SaveAssets();

        // which graphics level the phone starts in
        GraphicsSetup.SetAndroidQuality(old ? GraphicsSetup.LiteQualityLevel : GraphicsSetup.MobileQualityLevel);
        Debug.Log($"[Setup] Android build: {(old ? "old phones" : "standard")}, architectures {PlayerSettings.Android.targetArchitectures}, " +
                  $"min API {(int)PlayerSettings.Android.minSdkVersion}, starts in quality '{(old ? GraphicsSetup.LiteQualityLevel : GraphicsSetup.MobileQualityLevel)}'");
        bool ok = false;
        try
        {
            ok = Run(Path.Combine(dir, old ? "PrdivyZavod-old.apk" : "PrdivyZavod.apk"), BuildTarget.Android);
        }
        finally
        {
            // leave the project the way the standard build has it
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = normalMin;
            GraphicsSetup.SetAndroidQuality(GraphicsSetup.MobileQualityLevel);
            AssetDatabase.SaveAssets();
        }
        if (!ok && Application.isBatchMode) EditorApplication.Exit(3);
    }

    static bool Run(string path, BuildTarget target)
    {
        // the name and picture the game goes by (window title, the icon on a phone)
        PlayerSettings.productName = "Prdivý závod";
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Icon.png");   // Tools/assets/make_icon.py
        if (icon != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        else Debug.LogWarning("[Setup] Assets/Icon.png is missing: building with the default icon");
        AssetDatabase.SaveAssets();

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Main.unity" },
            locationPathName = path,
            target = target,
            options = BuildOptions.None,
        });

        var s = report.summary;
        Debug.Log($"[Setup] build {s.result}: {s.totalSize / (1024 * 1024)} MB, {s.totalErrors} errors, {s.totalWarnings} warnings, {s.totalTime.TotalSeconds:0}s -> {s.outputPath}");
        Report(s);
        return s.result == BuildResult.Succeeded;
    }

    // The build on GitHub (GameCI, .github/workflows/build.yml) decides whether a build
    // worked by reading what it printed: it looks for this block and for the words
    // "Build succeeded!", the way GameCI's own build script prints them. Without them it
    // calls a finished build a failure.
    static void Report(BuildSummary s)
    {
        Console.WriteLine(
            "\n" +
            "###########################\n" +
            "#      Build results      #\n" +
            "###########################\n" +
            "\n" +
            $"Duration: {s.totalTime}\n" +
            $"Warnings: {s.totalWarnings}\n" +
            $"Errors: {s.totalErrors}\n" +
            $"Size: {s.totalSize} bytes\n");
        Console.WriteLine(s.result == BuildResult.Succeeded
            ? "Build succeeded!"
            : $"Build {s.result.ToString().ToLowerInvariant()}!");
    }
}

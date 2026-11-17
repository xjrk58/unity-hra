using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// One-shot project setup for the realistic look. Safe to re-run at any time.
///
///   Tools ▸ Prdivý závod ▸ Rebuild graphics settings and materials
///   (or headless:  Unity -batchmode -projectPath . -executeMethod GraphicsSetup.Apply -quit)
///
/// 1. Creates the URP pipeline + renderer assets (HDR, MSAA, soft cascaded
///    shadows, Forward+, SSAO) and makes them the active pipeline; switches the
///    project to linear colour space. A second, lighter pair is created for
///    phones and attached to the quality level Android and iOS start in, and a
///    third, lightest one for old phones (quality level "Low").
/// 2. Fixes texture import settings (normal maps, linear mask maps, the HDRI sky).
/// 3. Generates every material the game loads from Resources/Materials, so the
///    shader variants they need are also included in player builds.
/// </summary>
public static class GraphicsSetup
{
    const string SettingsDir = "Assets/Settings";
    const string PipelinePath = SettingsDir + "/GamePipeline.asset";
    const string RendererPath = SettingsDir + "/GameRenderer.asset";
    const string MobilePipelinePath = SettingsDir + "/GameMobilePipeline.asset";
    const string MobileRendererPath = SettingsDir + "/GameMobileRenderer.asset";

    /// <summary>The quality level phones start in; it carries the lighter pipeline.</summary>
    public const string MobileQualityLevel = "Medium";

    const string LitePipelinePath = SettingsDir + "/GameLitePipeline.asset";
    const string LiteRendererPath = SettingsDir + "/GameLiteRenderer.asset";

    /// <summary>The quality level the build for old phones starts in; it carries the lightest pipeline.</summary>
    public const string LiteQualityLevel = "Low";
    const string TexDir = "Assets/Resources/Textures";
    const string MatDir = "Assets/Resources/Materials";
    const string SkyTex = "Assets/Resources/Sky/Sky.hdr";

    [MenuItem("Tools/Prdivý závod/Rebuild graphics settings and materials")]
    public static void Apply()
    {
        SetupPipeline();
        FixTextureImports();
        BuildMaterials();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Setup] done");
    }

    // ------------------------------------------------------------------ pipeline

    enum Tier { Desktop, Mobile, Lite }

    static void SetupPipeline()
    {
        Directory.CreateDirectory(SettingsDir);
        AssetDatabase.Refresh();

        var pipeline = CreatePipeline(PipelinePath, RendererPath, Tier.Desktop);
        var mobile = CreatePipeline(MobilePipelinePath, MobileRendererPath, Tier.Mobile);
        var lite = CreatePipeline(LitePipelinePath, LiteRendererPath, Tier.Lite);

        GraphicsSettings.defaultRenderPipeline = pipeline;
        PlayerSettings.colorSpace = ColorSpace.Linear;
        SetupQualityLevel(MobileQualityLevel, mobile, 2, "Android", "iPhone");
        SetupQualityLevel(LiteQualityLevel, lite, 0);
        Debug.Log($"[Setup] pipeline: {GraphicsSettings.defaultRenderPipeline}, mobile: {mobile}, lite: {lite}");
    }

    // The desktop pipeline is the full look. The mobile one keeps the same lighting model
    // (HDR, Forward+, probes, the same post-processing) but drops what a phone GPU pays most
    // for: screen-space AO, half of the shadow cascades and resolution, and a fifth of the
    // rendered pixels in each direction. The lite one is for old phones: no anti-aliasing,
    // 60 % of the resolution, one small hard-edged shadow map.
    static UniversalRenderPipelineAsset CreatePipeline(string pipelinePath, string rendererPath, Tier tier)
    {
        bool desktop = tier == Tier.Desktop, lite = tier == Tier.Lite;
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
        if (renderer == null)
        {
            renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(renderer, rendererPath);
            ResourceReloader.ReloadAllNullIn(renderer, UniversalRenderPipelineAsset.packagePath);
            renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                UniversalRenderPipelineAsset.packagePath + "/Runtime/Data/PostProcessData.asset");
        }
        renderer.renderingMode = RenderingMode.ForwardPlus;
        if (desktop) EnsureSsao(renderer);
        EditorUtility.SetDirty(renderer);

        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
        if (pipeline == null)
        {
            pipeline = UniversalRenderPipelineAsset.Create(renderer);
            AssetDatabase.CreateAsset(pipeline, pipelinePath);
        }

        var so = new SerializedObject(pipeline);
        Set(so, "m_RequireDepthTexture", true);          // soft particles
        Set(so, "m_SupportsHDR", true);
        Set(so, "m_MSAA", desktop ? 4 : lite ? 1 : 2);
        Set(so, "m_RenderScale", desktop ? 1f : lite ? 0.6f : 0.8f);
        Set(so, "m_MainLightShadowsSupported", true);
        Set(so, "m_MainLightShadowmapResolution", desktop ? 4096 : lite ? 1024 : 2048);
        Set(so, "m_ShadowDistance", desktop ? 65f : lite ? 28f : 50f);
        Set(so, "m_ShadowCascadeCount", desktop ? 4 : lite ? 1 : 2);
        if (!desktop) Set(so, "m_Cascade2Split", 0.22f);
        Set(so, "m_Cascade4Split", new Vector3(0.09f, 0.24f, 0.52f));
        Set(so, "m_CascadeBorder", 0.12f);
        Set(so, "m_ShadowDepthBias", 0.6f);
        Set(so, "m_ShadowNormalBias", 0.6f);
        Set(so, "m_SoftShadowsSupported", !lite);
        Set(so, "m_SoftShadowQuality", desktop ? 3 : 1);  // high / low
        Set(so, "m_AdditionalLightsRenderingMode", 1);    // per pixel
        Set(so, "m_AdditionalLightsPerObjectLimit", 8);
        Set(so, "m_AdditionalLightShadowsSupported", false);
        Set(so, "m_ReflectionProbeBlending", !lite);
        Set(so, "m_ReflectionProbeBoxProjection", true);
        Set(so, "m_ColorGradingMode", 1);                 // HDR grading
        Set(so, "m_ColorGradingLutSize", lite ? 16 : 32);
        Set(so, "m_UseSRPBatcher", true);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(pipeline);
        return pipeline;
    }

    // Gives a quality level its own pipeline and lets it render the per-deck reflection
    // probes (off by default below "High"). The platforms named start in that level.
    static void SetupQualityLevel(string name, UniversalRenderPipelineAsset pipeline, int msaa, params string[] defaultFor)
    {
        var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset").FirstOrDefault();
        if (asset == null) { Debug.LogError("[Setup] no QualitySettings asset"); return; }
        var so = new SerializedObject(asset);
        var levels = so.FindProperty("m_QualitySettings");
        int index = -1;
        for (int i = 0; i < levels.arraySize; i++)
        {
            var level = levels.GetArrayElementAtIndex(i);
            if (level.FindPropertyRelative("name").stringValue != name) continue;
            index = i;
            level.FindPropertyRelative("customRenderPipeline").objectReferenceValue = pipeline;
            level.FindPropertyRelative("realtimeReflectionProbes").boolValue = true;
            level.FindPropertyRelative("antiAliasing").intValue = msaa;
        }
        if (index < 0) { Debug.LogError("[Setup] no quality level named " + name); return; }

        var defaults = so.FindProperty("m_PerPlatformDefaultQuality");
        string report = "";
        for (int i = 0; i < defaults.arraySize; i++)
        {
            var entry = defaults.GetArrayElementAtIndex(i);
            string platform = entry.FindPropertyRelative("first").stringValue;
            if (Array.IndexOf(defaultFor, platform) < 0) continue;
            entry.FindPropertyRelative("second").intValue = index;
            report += $" {platform}={index}";
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log($"[Setup] quality level '{name}' (index {index}) -> {pipeline.name};{report}");
    }

    /// <summary>
    /// Makes Android start in the named quality level (the build for old phones
    /// borrows this for the length of one build). Returns the level it started in before.
    /// </summary>
    public static string SetAndroidQuality(string name)
    {
        var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset").FirstOrDefault();
        var so = new SerializedObject(asset);
        var levels = so.FindProperty("m_QualitySettings");
        int index = -1;
        for (int i = 0; i < levels.arraySize; i++)
            if (levels.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == name) index = i;
        if (index < 0) throw new ArgumentException("no quality level named " + name);
        string before = name;
        var defaults = so.FindProperty("m_PerPlatformDefaultQuality");
        for (int i = 0; i < defaults.arraySize; i++)
        {
            var entry = defaults.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("first").stringValue != "Android") continue;
            int was = entry.FindPropertyRelative("second").intValue;
            if (was >= 0 && was < levels.arraySize) before = levels.GetArrayElementAtIndex(was).FindPropertyRelative("name").stringValue;
            entry.FindPropertyRelative("second").intValue = index;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        return before;
    }

    static void EnsureSsao(UniversalRendererData data)
    {
        var ssao = data.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>().FirstOrDefault();
        if (ssao == null)
        {
            ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
            ssao.name = "ScreenSpaceAmbientOcclusion";
            AssetDatabase.AddObjectToAsset(ssao, data);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ssao, out _, out long localId);

            var dso = new SerializedObject(data);
            var feats = dso.FindProperty("m_RendererFeatures");
            feats.arraySize++;
            feats.GetArrayElementAtIndex(feats.arraySize - 1).objectReferenceValue = ssao;
            var map = dso.FindProperty("m_RendererFeatureMap");
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            dso.ApplyModifiedPropertiesWithoutUndo();
        }

        var so = new SerializedObject(ssao);
        Set(so, "m_Settings.Downsample", false);
        Set(so, "m_Settings.AfterOpaque", false);
        Set(so, "m_Settings.Source", 1);                  // depth normals
        Set(so, "m_Settings.NormalSamples", 1);
        Set(so, "m_Settings.Intensity", 1.5f);
        Set(so, "m_Settings.DirectLightingStrength", 0.25f);
        Set(so, "m_Settings.Radius", 0.45f);
        Set(so, "m_Settings.Samples", 0);                 // high (12 samples)
        Set(so, "m_Settings.BlurQuality", 0);             // high
        Set(so, "m_Settings.Falloff", 80f);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(ssao);
    }

    static void Set(SerializedObject so, string path, object value)
    {
        var p = so.FindProperty(path);
        if (p == null) { Debug.LogWarning("[Setup] no property " + path + " on " + so.targetObject); return; }
        switch (value)
        {
            case bool b: p.boolValue = b; break;
            case int i: p.intValue = i; break;
            case float f: p.floatValue = f; break;
            case Vector3 v: p.vector3Value = v; break;
            default: throw new ArgumentException(path);
        }
    }

    // ------------------------------------------------------------------ textures

    static void FixTextureImports()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Texture", new[] { TexDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) continue;
            bool dirty = false;

            if (path.EndsWith("_normal.jpg"))
            {
                if (ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; dirty = true; }
            }
            else if (path.EndsWith("_mask.png"))
            {
                if (ti.sRGBTexture) { ti.sRGBTexture = false; dirty = true; }
            }
            else if (path.Contains("/Fx/"))
            {
                if (!ti.alphaIsTransparency) { ti.alphaIsTransparency = true; dirty = true; }
                if (ti.wrapMode != TextureWrapMode.Clamp) { ti.wrapMode = TextureWrapMode.Clamp; dirty = true; }
            }
            if (ti.anisoLevel != 8) { ti.anisoLevel = 8; dirty = true; }
            if (dirty) ti.SaveAndReimport();
        }

        var sky = AssetImporter.GetAtPath(SkyTex) as TextureImporter;
        if (sky != null)
        {
            bool dirty = sky.maxTextureSize != 4096 || sky.mipmapEnabled || sky.wrapModeV != TextureWrapMode.Clamp;
            sky.maxTextureSize = 4096;
            sky.mipmapEnabled = false;   // avoids the lat-long seam of the panoramic skybox
            sky.wrapModeU = TextureWrapMode.Repeat;
            sky.wrapModeV = TextureWrapMode.Clamp;

            // a 4K HDR panorama is too much memory for a phone: half the size there
            var android = sky.GetPlatformTextureSettings("Android");
            if (!android.overridden || android.maxTextureSize != 2048)
            {
                android.overridden = true;
                android.maxTextureSize = 2048;
                android.format = TextureImporterFormat.Automatic;
                sky.SetPlatformTextureSettings(android);
                dirty = true;
            }
            if (dirty) sky.SaveAndReimport();
        }
    }

    // ------------------------------------------------------------------ materials

    static void BuildMaterials()
    {
        Directory.CreateDirectory(MatDir);
        AssetDatabase.Refresh();

        // --- arena surfaces (CC0 texture sets from Poly Haven)
        Textured("ConcreteFloor", "ConcreteFloor", Color.white, normal: 1.0f);
        Textured("ConcreteWall", "ConcreteWall", new Color(0.92f, 0.92f, 0.92f), normal: 1.0f);
        Textured("Asphalt", "Asphalt", Color.white, normal: 1.2f);
        Textured("Ground", "Ground", new Color(0.85f, 0.9f, 0.8f), normal: 1.0f);
        Textured("TreadPlate", "TreadPlate", new Color(1f, 0.62f, 0.25f), normal: 1.0f);
        Textured("Leather", "Leather", Color.white, normal: 1.0f);
        Textured("Fabric", "Fabric", Color.white, normal: 1.3f);   // tinted per character at runtime

        // --- scanned props
        foreach (var p in new[] { "BarrelRed", "BarrelBlue", "Barrier", "Crate", "Box" })
            Textured("Prop_" + p, "Props/" + p, Color.white, normal: 1.0f);

        // --- plain PBR
        Plain("Steel", new Color(0.56f, 0.58f, 0.60f), metallic: 1f, smoothness: 0.52f);
        Plain("Brass", new Color(0.83f, 0.62f, 0.27f), metallic: 1f, smoothness: 0.68f);
        Plain("GoalPaint", new Color(0.95f, 0.36f, 0.03f), metallic: 0.1f, smoothness: 0.6f);
        Plain("HazardYellow", new Color(0.93f, 0.68f, 0.04f), metallic: 0f, smoothness: 0.45f);
        Plain("HazardBlack", new Color(0.035f, 0.035f, 0.04f), metallic: 0f, smoothness: 0.4f);
        Plain("PaintWhite", new Color(0.80f, 0.80f, 0.78f), metallic: 0f, smoothness: 0.3f);
        Plain("PaintYellow", new Color(0.86f, 0.63f, 0.05f), metallic: 0f, smoothness: 0.3f);
        Plain("LampBody", new Color(0.86f, 0.86f, 0.84f), metallic: 0.2f, smoothness: 0.55f);
        Plain("Skin", new Color(0.87f, 0.63f, 0.50f), metallic: 0f, smoothness: 0.42f);
        Plain("Rubber", new Color(0.06f, 0.06f, 0.065f), metallic: 0f, smoothness: 0.38f);
        Plain("Glove", new Color(0.88f, 0.83f, 0.70f), metallic: 0f, smoothness: 0.3f);
        Plain("EyeWhite", new Color(0.95f, 0.95f, 0.93f), metallic: 0f, smoothness: 0.88f);
        Plain("Pupil", new Color(0.02f, 0.02f, 0.025f), metallic: 0f, smoothness: 0.92f);
        Plain("Hair", new Color(0.23f, 0.14f, 0.07f), metallic: 0f, smoothness: 0.35f);

        // --- the fart monster (Tools/blender/build_monster.py)
        Plain("MonsterBody", new Color(0.026f, 0.042f, 0.028f), metallic: 0f, smoothness: 0.46f);
        Plain("MonsterTooth", new Color(0.80f, 0.74f, 0.52f), metallic: 0f, smoothness: 0.6f);
        Plain("MonsterClaw", new Color(0.13f, 0.11f, 0.08f), metallic: 0f, smoothness: 0.62f);
        Emissive("MonsterEye", new Color(1f, 0.09f, 0f), 18f);
        Emissive("MonsterMaw", new Color(0.30f, 1f, 0.05f), 3f);

        // --- emissive
        Emissive("LampGlow", new Color(1f, 0.96f, 0.88f), 9f);
        Emissive("GoalGlow", new Color(1f, 0.42f, 0.04f), 7f);
        Emissive("Gas", new Color(0.45f, 0.95f, 0.15f), 3.2f);

        // --- glass
        Lit("TankGlass", m =>
        {
            m.SetFloat("_Surface", 1f);     // transparent
            m.SetFloat("_Blend", 0f);       // alpha
            m.SetColor("_BaseColor", new Color(0.82f, 0.95f, 1f, 0.16f));
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", 0.96f);
        });

        // --- particles
        var puff = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/Fx/Puff.png");
        Particle("GasPuff", puff, additive: false);
        Particle("GasGlow", puff, additive: true);

        // --- sky
        var skyTex = AssetDatabase.LoadAssetAtPath<Texture>(SkyTex);
        var sky = LoadOrCreate("Sky", "Skybox/Panoramic");
        sky.SetTexture("_MainTex", skyTex);
        sky.SetFloat("_Mapping", 1f);       // latitude-longitude layout
        sky.SetFloat("_ImageType", 0f);     // 360 degrees
        sky.DisableKeyword("_MAPPING_6_FRAMES_LAYOUT");
        sky.SetFloat("_Exposure", 1f);
        sky.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 0.5f));
        EditorUtility.SetDirty(sky);

        Debug.Log("[Setup] materials: " + AssetDatabase.FindAssets("t:Material", new[] { MatDir }).Length);
    }

    static Material LoadOrCreate(string name, string shaderName)
    {
        string path = MatDir + "/" + name + ".mat";
        var shader = Shader.Find(shaderName);
        if (shader == null) throw new Exception("shader not found: " + shaderName);
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        else if (m.shader != shader) m.shader = shader;
        return m;
    }

    static Material Lit(string name, Action<Material> setup)
    {
        var m = LoadOrCreate(name, "Universal Render Pipeline/Lit");
        m.SetFloat("_WorkflowMode", 1f);    // metallic
        setup(m);
        BaseShaderGUI.SetMaterialKeywords(m, LitGUI.SetMaterialKeywords);
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    static void Textured(string name, string texSet, Color tint, float normal)
    {
        var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{texSet}_albedo.jpg");
        var nrm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{texSet}_normal.jpg");
        var mask = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{texSet}_mask.png");
        if (albedo == null || nrm == null || mask == null)
        {
            Debug.LogError("[Setup] missing textures for " + texSet);
            return;
        }
        Lit(name, m =>
        {
            m.SetTexture("_BaseMap", albedo);
            m.SetColor("_BaseColor", tint);
            m.SetTexture("_BumpMap", nrm);
            m.SetFloat("_BumpScale", normal);
            m.SetTexture("_MetallicGlossMap", mask);     // R = metallic, A = smoothness
            m.SetFloat("_Metallic", 1f);
            m.SetFloat("_Smoothness", 1f);               // scales the map's alpha
            m.SetFloat("_SmoothnessTextureChannel", 0f);
            m.SetTexture("_OcclusionMap", mask);         // G = ambient occlusion
            m.SetFloat("_OcclusionStrength", 1f);
        });
    }

    static void Plain(string name, Color color, float metallic, float smoothness)
    {
        Lit(name, m =>
        {
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smoothness);
        });
    }

    static void Emissive(string name, Color color, float intensity)
    {
        Lit(name, m =>
        {
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", 0.5f);
            m.SetColor("_EmissionColor", color.linear * intensity);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        });
    }

    static void Particle(string name, Texture2D tex, bool additive)
    {
        var m = LoadOrCreate(name, "Universal Render Pipeline/Particles/Unlit");
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Surface", 1f);                      // transparent
        m.SetFloat("_Blend", additive ? 2f : 0f);        // additive / alpha
        m.SetFloat("_ColorMode", 0f);                    // multiply by vertex colour
        m.SetFloat("_SoftParticlesEnabled", 1f);
        m.SetFloat("_SoftParticlesNearFadeDistance", 0f);
        m.SetFloat("_SoftParticlesFarFadeDistance", 0.8f);
        BaseShaderGUI.SetMaterialKeywords(m, null, ParticleGUI.SetMaterialKeywords);
        EditorUtility.SetDirty(m);
    }
}

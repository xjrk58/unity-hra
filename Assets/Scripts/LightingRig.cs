using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Everything that makes the scene look photographic rather than "default
/// Unity": an HDRI sky that also lights the scene, a sun aligned with the sun
/// in that sky, distance haze, per-deck reflection probes captured from the
/// built world, and a filmic post-processing stack.
///
/// The whole look can be dragged from that sunny day into a sick green gloom
/// (<see cref="SetMood"/>): that is what the fart monster does when it comes.
/// </summary>
public class LightingRig : MonoBehaviour
{
    // Where the sun sits in Resources/Sky/Sky.hdr (measured from the image):
    // 29° above the horizon, at u = 0.6007 across the panorama.
    const float SunElevation = 29.05f;
    const float SunU = 0.6007f;

    // Compass direction the sunlight should come FROM in the arena (XZ plane,
    // degrees from +X toward +Z). Low, from the front-left of the spawn point,
    // so it rakes across the decks and reaches deep under the slabs.
    const float SunAzimuth = 152f;

    public static LightingRig Instance { get; private set; }
    public Light Sun { get; private set; }

    static int liteLevel = -2;

    /// <summary>
    /// True in the lightest graphics level ("Low", what the build for old phones
    /// starts in): no bloom, and only the lamps of the deck the camera is on are
    /// lit, which keeps the scene inside the 16 lights an old phone's graphics
    /// chip can take.
    /// </summary>
    public static bool Lite
    {
        get
        {
            if (liteLevel == -2) liteLevel = System.Array.IndexOf(QualitySettings.names, "Low");
            return liteLevel >= 0 && QualitySettings.GetQualityLevel() == liteLevel;
        }
    }

    bool tierLite;

    ReflectionProbe[] probes;
    int framesUntilProbes = 2;

    // the day, as set up below; SetMood blends from these toward the gloom
    static readonly Color DaySun = new Color(1f, 0.92f, 0.78f);
    static readonly Color DaySky = new Color(0.31f, 0.38f, 0.50f), DayEquator = new Color(0.33f, 0.36f, 0.41f), DayGround = new Color(0.30f, 0.275f, 0.225f);
    static readonly Color DayFog = new Color(0.60f, 0.72f, 0.84f);
    const float DaySunIntensity = 2.3f, DayFogDensity = 0.004f, DayExposure = 0.55f, DaySaturation = 6f, DayVignette = 0.2f, DayBloom = 0.42f;

    static readonly Color GloomSun = new Color(0.50f, 0.85f, 0.60f);
    static readonly Color GloomSky = new Color(0.050f, 0.090f, 0.062f), GloomEquator = new Color(0.040f, 0.066f, 0.045f), GloomGround = new Color(0.036f, 0.048f, 0.030f);
    static readonly Color GloomFog = new Color(0.030f, 0.058f, 0.038f);

    Material sky;
    ColorAdjustments grade;
    Bloom bloom;
    Vignette vignette;
    Color lampEmission;
    float envDread = -1f;

    /// <summary>How dark it currently is: 0 = the sunny day, 1 = the monster's gloom.</summary>
    public float Dread { get; private set; }

    public static LightingRig Build()
    {
        var go = new GameObject("Lighting");
        Instance = go.AddComponent<LightingRig>();
        Instance.Setup();
        return Instance;
    }

    void Setup()
    {
        // --- sky: rotate the panorama so its sun lands on SunAzimuth
        // (Skybox/Panoramic maps u = 0.5 - atan2(z, x) / 2π, then spins by _Rotation)
        float texAzimuth = (0.5f - SunU) * 360f;
        sky = new Material(Mats.Get("Sky"));
        sky.SetFloat("_Rotation", Mathf.Repeat(SunAzimuth - texAzimuth, 360f));
        sky.SetFloat("_Exposure", 1f);
        RenderSettings.skybox = sky;

        // --- sun
        float az = SunAzimuth * Mathf.Deg2Rad, el = SunElevation * Mathf.Deg2Rad;
        Vector3 toSun = new Vector3(Mathf.Cos(el) * Mathf.Cos(az), Mathf.Sin(el), Mathf.Cos(el) * Mathf.Sin(az));
        var sunGO = new GameObject("Sun");
        sunGO.transform.SetParent(transform, false);
        sunGO.transform.rotation = Quaternion.LookRotation(-toSun, Vector3.up);
        Sun = sunGO.AddComponent<Light>();
        Sun.type = LightType.Directional;
        Sun.color = DaySun;
        Sun.intensity = DaySunIntensity;
        Sun.shadows = LightShadows.Soft;
        Sun.shadowStrength = 1f;
        Sun.shadowBias = 0.04f;
        Sun.shadowNormalBias = 0.5f;
        RenderSettings.sun = Sun;

        // --- ambient: a three-colour gradient measured from the same sky. Nearly all play
        // happens under a slab, where light arrives sideways through the open edges and
        // bounces off sun-lit concrete, so the gradient is flatter and more neutral than
        // the open sky would be: cool from above, warm bounce from below (the soffits).
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = DaySky.gamma;
        RenderSettings.ambientEquatorColor = DayEquator.gamma;
        RenderSettings.ambientGroundColor = DayGround.gamma;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        RenderSettings.reflectionIntensity = 1f;
        DynamicGI.UpdateEnvironment();

        // --- aerial perspective: the far ground melts into the horizon haze
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = DayFog;
        RenderSettings.fogDensity = DayFogDensity;

        BuildProbes();
        BuildPostProcessing();
    }

    // One box-projected probe per deck so glossy surfaces reflect the room they
    // are actually in (bright openings, dark soffit) instead of open sky.
    void BuildProbes()
    {
        probes = new ReflectionProbe[Arena.FloorCount];
        for (int f = 0; f < Arena.FloorCount; f++)
        {
            bool roof = f == Arena.FloorCount - 1;
            var go = new GameObject("ReflectionProbe" + f);
            go.transform.SetParent(transform, false);
            // deck centre: clear of both the ramp going up and the hole coming up
            go.transform.position = new Vector3(0f, Arena.FloorY(f) + 2.15f, 0f);
            var p = go.AddComponent<ReflectionProbe>();
            p.mode = ReflectionProbeMode.Realtime;
            p.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            p.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
            p.resolution = Lite ? 64 : 128;
            p.hdr = true;
            p.boxProjection = !roof;
            // the box spans floor surface to soffit (plus a sliver so both surfaces are inside)
            float height = roof ? 40f : Arena.FloorHeight - Arena.SlabThickness + 0.3f;
            p.size = new Vector3(Arena.Half * 2f + 1f, height, Arena.Half * 2f + 1f);
            p.center = new Vector3(0f, roof ? height * 0.5f - 2.4f : 0f, 0f);
            p.nearClipPlane = 0.1f;
            p.farClipPlane = 600f;
            p.clearFlags = ReflectionProbeClearFlags.Skybox;
            p.importance = 1;
            p.blendDistance = 0.6f;
            probes[f] = p;
        }
    }

    void BuildPostProcessing()
    {
        var go = new GameObject("PostProcessing");
        go.transform.SetParent(transform, false);
        var volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        volume.profile = profile;

        var tone = profile.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.ACES);

        grade = profile.Add<ColorAdjustments>(true);
        grade.postExposure.Override(DayExposure);
        grade.contrast.Override(6f);
        grade.saturation.Override(DaySaturation);

        var wb = profile.Add<WhiteBalance>(true);
        wb.temperature.Override(4f);

        bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(1.05f);
        bloom.intensity.Override(DayBloom);
        bloom.scatter.Override(0.68f);
        bloom.clamp.Override(40f);
        bloom.highQualityFiltering.Override(!Application.isMobilePlatform);    // bicubic upsampling: too dear for a phone

        vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(DayVignette);
        vignette.smoothness.Override(0.45f);
    }

    /// <summary>Turns on post-processing for a camera created from code.</summary>
    public static void ConfigureCamera(Camera cam)
    {
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.allowHDR = true;
        cam.allowMSAA = true;
        cam.nearClipPlane = 0.08f;
        cam.farClipPlane = 1200f;
        var data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        data.renderShadows = true;
        data.antialiasing = AntialiasingMode.None;      // MSAA from the pipeline asset
        data.dithering = true;
    }

    void Update()
    {
        ApplyTier();

        // capture the probes once the whole world (built this frame) exists
        if (framesUntilProbes < 0) return;
        if (framesUntilProbes-- > 0) return;
        RenderProbes();
    }

    // What the lightest graphics level leaves out (see Lite).
    void ApplyTier()
    {
        bool lite = Lite;
        if (lite != tierLite)
        {
            tierLite = lite;
            if (bloom != null) bloom.active = !lite;
            if (!lite)
                foreach (var l in ArenaBuilder.LampLights)
                    if (l != null) l.enabled = true;
        }
        if (!lite) return;
        var cam = Camera.main;
        if (cam == null) return;
        int deck = Arena.FloorOfFeet(cam.transform.position.y - 1.65f);
        foreach (var l in ArenaBuilder.LampLights)
            if (l != null) l.enabled = Mathf.FloorToInt(l.transform.position.y / Arena.FloorHeight) == deck;
    }

    /// <summary>
    /// Sets the mood of the whole scene. <paramref name="dread"/> 0 is the sunny day
    /// and 1 the monster's gloom: the sun and the sky go out, a thick green murk
    /// closes in and the colour drains away. <paramref name="lamps"/> is the
    /// brightness of the ceiling lamps (1 = normal, 0 = dead), for flickering.
    /// </summary>
    public void SetMood(float dread, float lamps)
    {
        float d = Mathf.Clamp01(dread);
        Dread = d;
        Sun.intensity = Mathf.Lerp(DaySunIntensity, 0.10f, d);
        Sun.color = Color.Lerp(DaySun, GloomSun, d);
        sky.SetFloat("_Exposure", Mathf.Lerp(1f, 0.045f, d));
        RenderSettings.ambientSkyColor = Color.Lerp(DaySky, GloomSky, d).gamma;
        RenderSettings.ambientEquatorColor = Color.Lerp(DayEquator, GloomEquator, d).gamma;
        RenderSettings.ambientGroundColor = Color.Lerp(DayGround, GloomGround, d).gamma;
        RenderSettings.fogColor = Color.Lerp(DayFog, GloomFog, d);
        RenderSettings.fogDensity = Mathf.Lerp(DayFogDensity, 0.036f, d);

        // reflections were captured in daylight: turn them down with the light
        float reflect = Mathf.Lerp(1f, 0.08f, d);
        RenderSettings.reflectionIntensity = reflect;
        if (probes != null)
            foreach (var p in probes)
                if (p != null) p.intensity = reflect;

        if (grade != null)
        {
            grade.postExposure.value = Mathf.Lerp(DayExposure, 0.32f, d);
            grade.saturation.value = Mathf.Lerp(DaySaturation, -22f, d);
            vignette.intensity.value = Mathf.Lerp(DayVignette, 0.44f, d);
            bloom.intensity.value = Mathf.Lerp(DayBloom, 0.8f, d);
        }

        float level = Mathf.Clamp01(lamps);
        foreach (var l in ArenaBuilder.LampLights)
            if (l != null) l.intensity = ArenaBuilder.LampIntensity * level;
        if (ArenaBuilder.LampGlow != null)
        {
            if (lampEmission == default) lampEmission = ArenaBuilder.LampGlow.GetColor("_EmissionColor");
            ArenaBuilder.LampGlow.SetColor("_EmissionColor", lampEmission * level);
        }

        // the ambient probe follows the colours above only when asked to
        if (Mathf.Abs(d - envDread) > 0.02f)
        {
            envDread = d;
            DynamicGI.UpdateEnvironment();
        }
    }

    public void RenderProbes()
    {
        DynamicGI.UpdateEnvironment();
        if (probes == null) return;
        foreach (var p in probes)
            if (p != null) p.RenderProbe();
    }
}

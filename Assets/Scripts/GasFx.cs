using UnityEngine;

/// <summary>
/// Gas effects built from soft, depth-faded particle sprites: the puff when a
/// fart is expelled, the jet of a fart blast, the cloud a knocked-out rival
/// vanishes into, and the swirling wisp that marks a pickup.
/// </summary>
public static class GasFx
{
    static readonly Color GasGreen = new Color(0.56f, 0.80f, 0.20f);

    static Color GasColor(Color tint, float alpha)
    {
        Color c = Color.Lerp(GasGreen, tint, 0.28f);
        c.a = alpha;
        return c;
    }

    // Creates an inactive, fully reset particle system; the caller configures it
    // and then activates the GameObject (changing e.g. duration on a playing
    // system is not allowed).
    static ParticleSystem Make(string name, Vector3 position, Transform parent, string material)
    {
        var go = new GameObject(name);
        go.SetActive(false);
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.position = position;
        var ps = go.AddComponent<ParticleSystem>();
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = Mats.Get(material);
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.sortMode = ParticleSystemSortMode.Distance;
        r.minParticleSize = 0f;
        r.maxParticleSize = 3f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.15f;

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.35f;
        noise.frequency = 0.55f;
        noise.scrollSpeed = 0.35f;
        noise.damping = true;
        return ps;
    }

    static void Fade(ParticleSystem ps, Color color, float peakAlpha, float peakAt = 0.15f)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        Color pale = Color.Lerp(color, Color.white, 0.25f);
        g.SetKeys(
            new[] { new GradientColorKey(color, 0f), new GradientColorKey(pale, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peakAlpha, peakAt), new GradientAlphaKey(peakAlpha * 0.55f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
    }

    static void Grow(ParticleSystem ps, float from, float to)
    {
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, from, 0f, (to - from) * 2.2f), new Keyframe(1f, to, 0f, 0f)));
    }

    /// <summary>One-shot puff of gas that billows out, rises and thins away.</summary>
    public static void Burst(Vector3 position, Color tint, float scale)
    {
        var ps = Make("GasBurst", position, null, "GasPuff");
        var main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f * scale, 1.3f * scale);
        main.startSize = new ParticleSystem.MinMaxCurve(0.34f * scale, 0.66f * scale);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = Color.white;
        main.gravityModifier = -0.035f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 48;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.RoundToInt(9 + 4 * scale)) });

        var drag = ps.limitVelocityOverLifetime;
        drag.enabled = true;
        drag.limit = 0.25f;
        drag.dampen = 0.09f;

        Grow(ps, 0.55f, 2.4f);
        Fade(ps, GasColor(tint, 1f), 0.5f);
        ps.gameObject.SetActive(true);
    }

    /// <summary>A fast stream of gas from the attacker to the target.</summary>
    public static void Jet(Vector3 from, Vector3 to, Color tint)
    {
        Vector3 d = to - from;
        float dist = d.magnitude;
        if (dist < 0.01f) return;
        var ps = Make("GasJet", from + d.normalized * 0.5f, null, "GasPuff");
        ps.transform.rotation = Quaternion.LookRotation(d);
        float travel = 0.38f;

        var main = ps.main;
        main.duration = 0.3f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(travel * 0.9f, travel * 1.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(dist / travel * 0.75f, dist / travel);
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 90;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var em = ps.emission;
        em.rateOverTime = 220f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 2.5f;
        shape.radius = 0.08f;

        var noise = ps.noise;
        noise.strength = 0.15f;

        Grow(ps, 0.6f, 3.2f);
        Fade(ps, GasColor(tint, 1f), 0.42f, 0.1f);
        ps.gameObject.SetActive(true);
    }

    /// <summary>A cloud that hangs on the spot for <paramref name="seconds"/>.</summary>
    public static void Linger(Vector3 position, float seconds)
    {
        var ps = Make("GasCloud", position, null, "GasPuff");
        var main = ps.main;
        main.duration = Mathf.Max(0.5f, seconds - 1.2f);
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 2.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = -0.01f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var em = ps.emission;
        em.rateOverTime = 8f;

        var shape = ps.shape;
        shape.radius = 0.45f;

        Grow(ps, 0.7f, 1.7f);
        Fade(ps, GasColor(Color.white, 1f), 0.3f, 0.25f);
        ps.gameObject.SetActive(true);
    }

    /// <summary>
    /// The dark, boiling smoke the monster is wrapped in. It is left behind as the
    /// monster moves (world space), so a chase draws a trail.
    /// </summary>
    public static ParticleSystem MonsterSmoke(Transform parent)
    {
        var ps = Make("MonsterSmoke", parent.position + Vector3.up * 1.3f, parent, "GasPuff");
        var main = ps.main;
        main.duration = 3f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.7f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = -0.02f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 70;

        var em = ps.emission;
        em.rateOverTime = 20f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(1.5f, 2.3f, 1.1f);

        var noise = ps.noise;
        noise.strength = 0.6f;
        noise.frequency = 0.7f;

        Grow(ps, 0.6f, 1.9f);
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(new Color(0.02f, 0.05f, 0.02f), 0f), new GradientColorKey(new Color(0.10f, 0.20f, 0.05f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.62f, 0.2f), new GradientAlphaKey(0.35f, 0.65f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        ps.gameObject.SetActive(true);
        return ps;
    }

    /// <summary>A big dark cloud: the monster boiling up out of the floor or sinking back into it.</summary>
    public static void MonsterBurst(Vector3 position)
    {
        var ps = Make("MonsterBurst", position, null, "GasPuff");
        var main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 3.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = -0.05f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 34) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.6f;
        shape.rotation = new Vector3(-90f, 0f, 0f);      // dome opening upward

        var drag = ps.limitVelocityOverLifetime;
        drag.enabled = true;
        drag.limit = 0.4f;
        drag.dampen = 0.07f;

        Grow(ps, 0.5f, 2.2f);
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(new Color(0.03f, 0.07f, 0.02f), 0f), new GradientColorKey(new Color(0.16f, 0.30f, 0.07f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.7f, 0.12f), new GradientAlphaKey(0.4f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        ps.gameObject.SetActive(true);
    }

    // ------------------------------------------------------------------ rainbow (unicorns)

    static readonly Color[] Rainbow =
    {
        new Color(0.95f, 0.15f, 0.15f), new Color(1f, 0.55f, 0.10f), new Color(1f, 0.90f, 0.15f),
        new Color(0.25f, 0.85f, 0.25f), new Color(0.20f, 0.50f, 1f), new Color(0.60f, 0.30f, 0.90f),
    };

    public const int RainbowBands = 6;

    public static Color RainbowColor(int i) => Rainbow[((i % Rainbow.Length) + Rainbow.Length) % Rainbow.Length];

    /// <summary>The six colours side by side, at the given opacity.</summary>
    public static Gradient RainbowGradient(float alpha)
    {
        var g = new Gradient();
        var keys = new GradientColorKey[Rainbow.Length];
        for (int i = 0; i < Rainbow.Length; i++) keys[i] = new GradientColorKey(Rainbow[i], i / (float)(Rainbow.Length - 1));
        g.SetKeys(keys, new[] { new GradientAlphaKey(alpha, 0f), new GradientAlphaKey(alpha, 1f) });
        return g;
    }

    // Particles that each take one of the rainbow's colours and only fade.
    static void RainbowTint(ParticleSystem ps, float peakAlpha)
    {
        var main = ps.main;
        main.startColor = new ParticleSystem.MinMaxGradient(RainbowGradient(1f)) { mode = ParticleSystemGradientMode.RandomColor };
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peakAlpha, 0.15f), new GradientAlphaKey(peakAlpha * 0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
    }

    /// <summary>
    /// A unicorn's fart: a rainbow that arcs out of its rear and comes down on
    /// the floor at <paramref name="to"/>, a cloud of coloured puffs and a
    /// shower of sparks - and more sparks where the rainbow lands.
    /// </summary>
    public static void RainbowFart(Vector3 from, Vector3 to, Vector3 back)
    {
        RainbowArc.Spawn(from, to, back);

        var puffs = Make("RainbowPuffs", from, null, "GasPuff");
        var main = puffs.main;
        main.duration = 1f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = -0.04f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 40;
        main.stopAction = ParticleSystemStopAction.Destroy;
        var em = puffs.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 22) });
        var shape = puffs.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 28f;
        shape.radius = 0.12f;
        puffs.transform.rotation = Quaternion.LookRotation(back + Vector3.up * 0.35f);
        var drag = puffs.limitVelocityOverLifetime;
        drag.enabled = true;
        drag.limit = 0.3f;
        drag.dampen = 0.08f;
        Grow(puffs, 0.6f, 2.2f);
        RainbowTint(puffs, 0.75f);
        puffs.gameObject.SetActive(true);

        RainbowSparks(from, back + Vector3.up * 0.8f, 40, 0f);
        RainbowSparks(to + Vector3.up * 0.15f, Vector3.up, 30, RainbowArc.GrowSeconds * 0.9f);
    }

    // A shower of little coloured lights, thrown one way.
    static void RainbowSparks(Vector3 at, Vector3 towards, int count, float delay)
    {
        var sparks = Make("RainbowSparks", at, null, "GasGlow");
        var sm = sparks.main;
        sm.duration = 1f + delay;
        sm.loop = false;
        sm.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.5f);
        sm.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4.5f);
        sm.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.22f);
        sm.gravityModifier = 0.35f;
        sm.simulationSpace = ParticleSystemSimulationSpace.World;
        sm.maxParticles = 60;
        sm.stopAction = ParticleSystemStopAction.Destroy;
        var se = sparks.emission;
        se.rateOverTime = 0f;
        se.SetBursts(new[] { new ParticleSystem.Burst(delay, (short)count) });
        var ss = sparks.shape;
        ss.shapeType = ParticleSystemShapeType.Cone;
        ss.angle = 40f;
        ss.radius = 0.1f;
        sparks.transform.rotation = Quaternion.LookRotation(towards);
        var sn = sparks.noise;
        sn.enabled = false;
        RainbowTint(sparks, 1f);
        sparks.gameObject.SetActive(true);
    }

    /// <summary>The looping, swirling wisp of a collectable fart. Returned inactive-safe: already playing.</summary>
    public static ParticleSystem Wisp(Transform parent, bool rainbow = false)
    {
        var ps = Make("Wisp", parent.position, parent, "GasPuff");
        var main = ps.main;
        main.duration = 2f;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 1.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.42f, 0.7f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = -0.02f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 36;

        var em = ps.emission;
        em.rateOverTime = 11f;

        var shape = ps.shape;
        shape.radius = 0.22f;

        var noise = ps.noise;
        noise.strength = 0.5f;
        noise.frequency = 0.9f;

        Grow(ps, 0.6f, 1.5f);
        if (rainbow) RainbowTint(ps, 0.75f);
        else Fade(ps, new Color(0.50f, 0.86f, 0.16f), 0.7f, 0.2f);
        ps.gameObject.SetActive(true);

        // faint additive halo so pickups read from across a deck
        var glow = Make("Halo", parent.position, ps.transform, "GasGlow");
        var gm = glow.main;
        gm.duration = 2f;
        gm.loop = true;
        gm.prewarm = true;
        gm.startLifetime = 1.6f;
        gm.startSpeed = 0f;
        gm.startSize = new ParticleSystem.MinMaxCurve(1.5f, 1.9f);
        gm.simulationSpace = ParticleSystemSimulationSpace.Local;
        gm.maxParticles = 6;
        var ge = glow.emission;
        ge.rateOverTime = 2f;
        var gs = glow.shape;
        gs.radius = 0.02f;
        var gn = glow.noise;
        gn.enabled = false;
        if (rainbow) RainbowTint(glow, 0.3f);
        else Fade(glow, new Color(0.45f, 0.9f, 0.12f), 0.26f, 0.5f);
        glow.gameObject.SetActive(true);
        return ps;
    }
}

/// <summary>
/// The rainbow a unicorn farts: six coloured bands that sweep out of its rear
/// in a high arch, come down on the floor where the rainbow fart will hang,
/// stay for a moment and fade.
///
/// The bands are flat ribbons that always face the camera. Seen from the side
/// they lie one inside the other, like a rainbow should; so that the rainbow
/// is not a single streak when seen from behind the unicorn, each band is also
/// shifted a little sideways, which fans them out in that view.
/// </summary>
public class RainbowArc : MonoBehaviour
{
    public const float GrowSeconds = 0.5f;
    const int Points = 36;
    const float Height = 2.5f, BandWidth = 0.13f, HoldSeconds = 2.0f, FadeSeconds = 1.2f;

    static Material bandMaterial;

    LineRenderer[] bands;
    Vector3 centre, along, side;
    float semiAxis, height, startAngle, age;

    public static void Spawn(Vector3 from, Vector3 to, Vector3 back)
    {
        var arc = new GameObject("RainbowArc").AddComponent<RainbowArc>();
        arc.Shape(from, to, back);
        arc.Build();
    }

    // Half an ellipse standing on the floor at the height of "to": it passes through
    // "from" on the way up and comes down at "to".
    void Shape(Vector3 from, Vector3 to, Vector3 back)
    {
        Vector3 flat = to - from;
        flat.y = 0f;
        float length = flat.magnitude;
        back.y = 0f;
        along = length > 0.3f ? flat / length : (back.sqrMagnitude > 0.0001f ? back.normalized : Vector3.back);
        side = Vector3.Cross(Vector3.up, along);
        float rise = Mathf.Max(0f, from.y - to.y);
        height = Mathf.Max(Height, rise + 0.6f);
        startAngle = Mathf.Asin(Mathf.Clamp01(rise / height));
        semiAxis = Mathf.Max(0.5f, length / (1f + Mathf.Cos(startAngle)));
        centre = from + along * (semiAxis * Mathf.Cos(startAngle));
        centre.y = to.y;
    }

    // The ribbon material (which fades out close to the camera, so that somebody running
    // to the rainbow's end does not get a screen full of stripes) with a texture of its
    // own: solid along the band, soft at its two edges and at its two ends.
    static Material BandMaterial()
    {
        if (bandMaterial != null) return bandMaterial;
        const int w = 64, h = 16;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "RainbowBand" };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            float v = (y + 0.5f) / h;
            float across = Mathf.SmoothStep(0f, 1f, Mathf.Min(v, 1f - v) / 0.22f);
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w;
                float ends = Mathf.SmoothStep(0f, 1f, u / 0.05f) * Mathf.SmoothStep(0f, 1f, (1f - u) / 0.12f);
                px[y * w + x] = new Color(1f, 1f, 1f, across * ends);
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        bandMaterial = new Material(Mats.Get("GasRibbon")) { name = "RainbowBand" };
        bandMaterial.SetTexture("_BaseMap", tex);
        return bandMaterial;
    }

    void Build()
    {
        var material = BandMaterial();
        bands = new LineRenderer[GasFx.RainbowBands];
        for (int i = 0; i < bands.Length; i++)
        {
            var go = new GameObject("Band" + i);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.sharedMaterial = material;
            lr.alignment = LineAlignment.View;
            lr.textureMode = LineTextureMode.Stretch;
            lr.widthMultiplier = BandWidth * 1.3f;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.positionCount = 0;
            bands[i] = lr;
        }
        Draw();
    }

    /// <summary>Lets the rainbow grow by this many seconds at once (dev tools: screenshots).</summary>
    public void Advance(float seconds)
    {
        age += seconds;
        Draw();
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= GrowSeconds + HoldSeconds + FadeSeconds)
        {
            Destroy(gameObject);
            return;
        }
        Draw();
    }

    void Draw()
    {
        float shown = Mathf.Clamp01(age / GrowSeconds);
        float alpha = age < GrowSeconds + HoldSeconds ? 1f : Mathf.Clamp01(1f - (age - GrowSeconds - HoldSeconds) / FadeSeconds);
        int n = Mathf.Max(2, Mathf.RoundToInt(shown * Points));
        for (int i = 0; i < bands.Length; i++)
        {
            float off = ((bands.Length - 1) * 0.5f - i) * BandWidth;      // red outside, violet inside
            var lr = bands[i];
            lr.positionCount = n;
            for (int k = 0; k < n; k++)
            {
                float a = startAngle + (Mathf.PI - startAngle) * shown * k / (n - 1);
                lr.SetPosition(k, centre - along * ((semiAxis + off) * Mathf.Cos(a)) + Vector3.up * ((height + off) * Mathf.Sin(a)) + side * off);
            }
            Color c = GasFx.RainbowColor(i);
            c.a = alpha * 0.95f;
            lr.startColor = lr.endColor = c;
        }
    }
}

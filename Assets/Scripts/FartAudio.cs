using UnityEngine;

/// <summary>
/// Fart sounds. The game plays real recordings from Resources/Audio/Farts (cut
/// from a sound-archive tape of several dozen different farts — see the
/// ATTRIBUTION file there), sorted by length: short ones for each fart expelled
/// or collected, long ones for the blast, the longest as the victory fanfare.
/// Clips are drawn without immediate repeats and with a slight random pitch, so
/// no two in a row sound alike.
///
/// If the recordings are missing, sounds are synthesised instead, modelled the
/// way a fart is physically produced: a tight flap valve that pops open and
/// shut (a jittery pulse train, 60–250 Hz, drifting and sagging in pitch), air
/// turbulence that only hisses while the valve is open, and the muffled
/// resonance of the body around it. Longer ones sputter and slow down into
/// separate "put-put" pops at the end.
/// </summary>
public static class FartAudio
{
    public const int SampleRate = 44100;

    public struct Spec
    {
        public float duration;     // seconds
        public float f0, f1;       // fundamental at the start / end (Hz)
        public float wobble;       // slow pitch drift (fraction)
        public float jitter;       // cycle-to-cycle pitch irregularity (fraction)
        public float shimmer;      // cycle-to-cycle loudness irregularity (0..1)
        public float sputter;      // chance per cycle that a flap is missed
        public float noise;        // turbulence mix (0..1)
        public float sharp;        // pulse sharpness (higher = raspier)
        public float formant1, formant2;
        public float lowpass;      // overall brightness (Hz)
        public float drive;        // saturation
        public bool putter;        // slows into separate pops at the end
    }

    // one pool of clips that never plays the same clip twice in a row
    class Bag
    {
        public AudioClip[] clips;
        int last = -1;
        public bool Empty => clips == null || clips.Length == 0;
        public AudioClip Next()
        {
            if (Empty) return null;
            int i = Random.Range(0, clips.Length);
            if (i == last && clips.Length > 1) i = (i + 1 + Random.Range(0, clips.Length - 1)) % clips.Length;
            last = i;
            return clips[i];
        }
    }

    static Bag expelBag, blastBag, pickupBag, fanfareBag;
    static AudioSource[] pool;
    static int nextSource;

    /// <summary>How many recorded clips were found (0 = everything is synthesised).</summary>
    public static int RecordedClips { get; private set; }

    /// <summary>Loads / generates the clip pools (once) and creates the audio sources (per scene).</summary>
    public static void Init()
    {
        if (expelBag == null || expelBag.Empty || expelBag.clips[0] == null) LoadClips();

        var root = new GameObject("Audio");
        pool = new AudioSource[10];
        for (int i = 0; i < pool.Length; i++)
        {
            var go = new GameObject("Voice" + i);
            go.transform.SetParent(root.transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.dopplerLevel = 0f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 2.5f;
            s.maxDistance = 42f;
            pool[i] = s;
        }
        nextSource = 0;
    }

    static void LoadClips()
    {
        var shorts = new System.Collections.Generic.List<AudioClip>();
        var mediums = new System.Collections.Generic.List<AudioClip>();
        var longs = new System.Collections.Generic.List<AudioClip>();
        var fanfares = new System.Collections.Generic.List<AudioClip>();
        var all = Resources.LoadAll<AudioClip>("Audio/Farts");
        System.Array.Sort(all, (x, y) => string.CompareOrdinal(x.name, y.name));
        foreach (var c in all)
        {
            if (c.name.StartsWith("short_")) shorts.Add(c);
            else if (c.name.StartsWith("medium_")) mediums.Add(c);
            else if (c.name.StartsWith("long_")) longs.Add(c);
            else if (c.name.StartsWith("fanfare_")) fanfares.Add(c);
        }
        RecordedClips = all.Length;

        // anything not covered by recordings is synthesised
        var rng = new System.Random(4711);
        if (shorts.Count == 0)
            for (int i = 0; i < 10; i++) shorts.Add(ToClip("FartShort" + i, Synthesize(ShortSpec(rng), rng)));
        if (longs.Count == 0)
            for (int i = 0; i < 5; i++) longs.Add(ToClip("FartLong" + i, Synthesize(LongSpec(rng), rng)));

        // collecting a fart: the briefest recordings, played quietly and pitched up
        var tiny = shorts.FindAll(c => c.length <= 0.45f);
        if (tiny.Count < 3)
        {
            tiny.Clear();
            for (int i = 0; i < 5; i++) tiny.Add(ToClip("FartSqueak" + i, Synthesize(SqueakSpec(rng), rng)));
        }

        var blasts = new System.Collections.Generic.List<AudioClip>(longs);
        blasts.AddRange(mediums);

        expelBag = new Bag { clips = shorts.ToArray() };
        blastBag = new Bag { clips = blasts.ToArray() };
        pickupBag = new Bag { clips = tiny.ToArray() };
        fanfareBag = new Bag { clips = fanfares.Count > 0 ? fanfares.ToArray() : longs.ToArray() };
    }

    static float R(System.Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);

    /// <summary>The quick "prd" for every fart expelled at the goal.</summary>
    public static Spec ShortSpec(System.Random rng)
    {
        float f0 = R(rng, 85f, 190f);
        return new Spec
        {
            duration = R(rng, 0.16f, 0.42f), f0 = f0, f1 = f0 * R(rng, 0.62f, 1.08f),
            wobble = 0.07f, jitter = R(rng, 0.03f, 0.08f), shimmer = R(rng, 0.25f, 0.5f), sputter = R(rng, 0f, 0.05f),
            noise = R(rng, 0.25f, 0.55f), sharp = R(rng, 4.5f, 8f),
            formant1 = R(rng, 230f, 400f), formant2 = R(rng, 800f, 1500f), lowpass = R(rng, 2400f, 4200f),
            drive = R(rng, 1.4f, 2.4f), putter = rng.NextDouble() < 0.35,
        };
    }

    /// <summary>The long, ripping blast used to knock a rival out.</summary>
    public static Spec LongSpec(System.Random rng)
    {
        float f0 = R(rng, 115f, 175f);
        return new Spec
        {
            duration = R(rng, 1.0f, 1.7f), f0 = f0, f1 = f0 * R(rng, 0.42f, 0.6f),
            wobble = 0.11f, jitter = R(rng, 0.04f, 0.09f), shimmer = R(rng, 0.3f, 0.55f), sputter = R(rng, 0.03f, 0.09f),
            noise = R(rng, 0.3f, 0.5f), sharp = R(rng, 5f, 9f),
            formant1 = R(rng, 220f, 340f), formant2 = R(rng, 750f, 1300f), lowpass = R(rng, 2400f, 3800f),
            drive = R(rng, 2f, 3f), putter = true,
        };
    }

    /// <summary>A tiny tight squeaker for collecting a pickup.</summary>
    public static Spec SqueakSpec(System.Random rng)
    {
        float f0 = R(rng, 290f, 480f);
        return new Spec
        {
            duration = R(rng, 0.09f, 0.19f), f0 = f0, f1 = f0 * R(rng, 1.05f, 1.45f),
            wobble = 0.04f, jitter = 0.02f, shimmer = 0.15f, sputter = 0f,
            noise = R(rng, 0.12f, 0.25f), sharp = R(rng, 3f, 5f),
            formant1 = R(rng, 420f, 600f), formant2 = R(rng, 1300f, 1900f), lowpass = R(rng, 3200f, 4500f),
            drive = 1.2f, putter = false,
        };
    }

    // Band-pass biquad (RBJ cookbook, constant 0 dB peak gain).
    struct BandPass
    {
        float b0, a1, a2, x1, x2, y1, y2;
        public BandPass(float freq, float q)
        {
            float w = 2f * Mathf.PI * freq / SampleRate;
            float alpha = Mathf.Sin(w) / (2f * q);
            float a0 = 1f + alpha;
            b0 = alpha / a0;
            a1 = -2f * Mathf.Cos(w) / a0;
            a2 = (1f - alpha) / a0;
            x1 = x2 = y1 = y2 = 0f;
        }
        public float Step(float x)
        {
            float y = b0 * x - b0 * x2 - a1 * y1 - a2 * y2;
            x2 = x1; x1 = x; y2 = y1; y1 = y;
            return y;
        }
    }

    static float OnePole(float cutoff) => 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / SampleRate);

    /// <summary>Renders one fart to mono samples in [-1, 1].</summary>
    public static float[] Synthesize(Spec s, System.Random rng)
    {
        int n = Mathf.Max(64, (int)(s.duration * SampleRate));
        var buf = new float[n];

        var res1 = new BandPass(s.formant1, 2.6f);
        var res2 = new BandPass(s.formant2, 3.8f);
        float kNoiseLo = OnePole(350f), kNoiseHi = OnePole(3200f), kLow = OnePole(s.lowpass), kDc = OnePole(32f);
        float nLo = 0f, nHi = 0f, lp1 = 0f, lp2 = 0f, dc = 0f;

        double phase = 0.0;
        float cycleAmp = 1f, jit = 0f;
        float wob = 0f, wobTarget = 0f, swell = 1f, swellTarget = 1f;
        int nextWobble = 0;
        float attack = 0.007f * SampleRate, release = Mathf.Min(0.07f, s.duration * 0.3f) * SampleRate;
        float peak = 1e-6f;

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;

            // pitch: sag from f0 to f1, optionally collapsing into slow pops at the very end
            float f = s.f0 * Mathf.Pow(s.f1 / s.f0, Mathf.Pow(t, 1.3f));
            if (s.putter && t > 0.7f)
            {
                float u = (t - 0.7f) / 0.3f;
                f *= Mathf.Lerp(1f, 0.3f, u * Mathf.Sqrt(u));
            }

            // slow random drift of pitch and loudness (muscle tension is never steady)
            if (i >= nextWobble)
            {
                wobTarget = R(rng, -1f, 1f);
                swellTarget = R(rng, 0.7f, 1f);
                nextWobble = i + (int)(R(rng, 0.02f, 0.06f) * SampleRate);
            }
            wob += (wobTarget - wob) * 0.0016f;
            swell += (swellTarget - swell) * 0.0016f;
            f *= 1f + s.wobble * wob + s.jitter * jit;

            phase += f / SampleRate;
            if (phase >= 1.0)
            {
                phase -= 1.0;
                jit = R(rng, -1f, 1f);
                cycleAmp = 1f - s.shimmer * (float)rng.NextDouble();
                if (rng.NextDouble() < s.sputter) cycleAmp *= 0.12f;   // a flap that didn't seal
            }

            // the valve: snaps open, then eases shut within the cycle
            float ph = (float)phase;
            float pulse = Mathf.Exp(-ph * s.sharp) * (1f - ph);

            // turbulence: band-limited noise, loud only while the valve is open
            float white = (float)rng.NextDouble() * 2f - 1f;
            nLo += (white - nLo) * kNoiseLo;
            nHi += ((white - nLo) - nHi) * kNoiseHi;
            float air = nHi * (0.2f + 0.8f * pulse) * 2.2f;

            float x = cycleAmp * swell * (pulse * (1f - 0.45f * s.noise) + air * s.noise);

            // body resonance and muffling
            float y = 0.5f * x + 1.4f * res1.Step(x) + 1.5f * res2.Step(x);
            lp1 += (y - lp1) * kLow;
            lp2 += (lp1 - lp2) * kLow;
            dc += (lp2 - dc) * kDc;
            y = lp2 - dc;

            float env = Mathf.Min(1f, i / attack) * Mathf.Min(1f, (n - 1 - i) / release);
            // gentle saturation only: hard clipping would iron out the cycle-to-cycle
            // irregularity that makes it sound organic instead of like a buzzer
            y = (float)System.Math.Tanh(y * s.drive * 0.7f) * env;
            buf[i] = y;
            float a = y < 0f ? -y : y;
            if (a > peak) peak = a;
        }

        float gain = 0.92f / peak;
        for (int i = 0; i < n; i++) buf[i] *= gain;
        return buf;
    }

    static AudioClip ToClip(string name, float[] samples)
    {
        var clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>One of the long recordings (the monster growls with them, slowed right down).</summary>
    public static AudioClip AnyLong() => blastBag != null ? blastBag.Next() : null;

    /// <summary>Wraps rendered samples in a clip.</summary>
    public static AudioClip MakeClip(string name, float[] samples) => ToClip(name, samples);

    // ------------------------------------------------------------------ playback

    public static void Expel(Vector3 position, bool isPlayer) =>
        Play(expelBag, position, isPlayer ? 0.85f : 0.7f, Random.Range(0.92f, 1.1f), isPlayer);

    public static void Blast(Vector3 position, bool isPlayer) =>
        Play(blastBag, position, 1f, Random.Range(0.94f, 1.07f), isPlayer);

    public static void Pickup(Vector3 position, bool isPlayer) =>
        Play(pickupBag, position, isPlayer ? 0.32f : 0.2f, Random.Range(1.25f, 1.6f), isPlayer);

    static AudioClip chime;

    /// <summary>
    /// A horse lets one go: long and low. A unicorn's is short and squeaky,
    /// with a little run of bells on top.
    /// </summary>
    public static void Animal(Vector3 position, bool unicorn)
    {
        if (!unicorn)
        {
            Play(blastBag, position, 0.9f, Random.Range(0.58f, 0.74f), false);
            return;
        }
        Play(expelBag, position, 0.7f, Random.Range(1.5f, 1.9f), false);
        if (chime == null) chime = ToClip("UnicornChime", RenderChime());
        Play(chime, position, 0.6f, Random.Range(0.96f, 1.06f), false);
    }

    /// <summary>Four rising bell notes (a C major chord, climbing).</summary>
    public static float[] RenderChime()
    {
        float[] notes = { 1046.5f, 1318.5f, 1568f, 2093f };
        int n = (int)(1.1f * SampleRate);
        var buf = new float[n];
        for (int k = 0; k < notes.Length; k++)
        {
            int start = (int)(k * 0.075f * SampleRate);
            for (int i = start; i < n; i++)
            {
                float t = (i - start) / (float)SampleRate;
                float env = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t / 0.22f);
                float w = 2f * Mathf.PI * notes[k] * t;
                buf[i] += env * (Mathf.Sin(w) + 0.3f * Mathf.Sin(2f * w) + 0.12f * Mathf.Sin(3.01f * w)) * 0.3f;
            }
        }
        float peak = 1e-6f;
        foreach (float v in buf) peak = Mathf.Max(peak, Mathf.Abs(v));
        for (int i = 0; i < n; i++) buf[i] *= 0.85f / peak;
        return buf;
    }

    /// <summary>A long triumphant one when somebody wins.</summary>
    public static void Fanfare() =>
        Play(fanfareBag, Vector3.zero, 1f, 1f, true);

    static void Play(Bag bag, Vector3 position, float volume, float pitch, bool flat)
    {
        if (bag != null) Play(bag.Next(), position, volume, pitch, flat);
    }

    static void Play(AudioClip clip, Vector3 position, float volume, float pitch, bool flat)
    {
        if (clip == null || pool == null) return;
        AudioSource s = null;
        for (int i = 0; i < pool.Length; i++)
        {
            var c = pool[(nextSource + i) % pool.Length];
            if (c == null) return;                 // scene was torn down
            if (!c.isPlaying) { s = c; nextSource = (nextSource + i + 1) % pool.Length; break; }
        }
        if (s == null) { s = pool[nextSource]; nextSource = (nextSource + 1) % pool.Length; }

        s.transform.position = position;
        s.spatialBlend = flat ? 0f : 1f;           // the player's own sounds are not positional
        s.clip = clip;
        s.volume = volume;
        s.pitch = pitch;
        s.Play();
    }
}

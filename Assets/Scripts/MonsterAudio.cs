using UnityEngine;

/// <summary>
/// What the fart monster sounds like. Everything is made here, from nothing but
/// arithmetic and the fart recordings, and built so that it still comes through
/// a phone's small speaker (which loses most of what lies below about 150 Hz):
///
///   drone      a low, beating hum with a breathy rasp; it comes from the monster
///              itself, so it tells you where it is and how close
///   growl      one of the long fart recordings played three times slower
///   sting      the stab of a dissonant chord when it bursts out of the floor
///   roar       a huge, ragged fart-roar when it catches somebody
///   shriek     when a blast drives it off
///   heartbeat  the player's own, faster the nearer the monster is
///
/// The monster has its own audio sources: the pooled voices used for ordinary
/// farts would cut these off.
/// </summary>
public class MonsterAudio : MonoBehaviour
{
    const int Rate = FartAudio.SampleRate;

    static AudioClip droneClip, heartClip, stingClip, roarClip, shriekClip;

    FartMonster monster;
    AudioSource drone, voice, growl, flat, heart;
    float nextGrowl, nextBeat;

    public static MonsterAudio Attach(FartMonster monster)
    {
        var a = monster.gameObject.AddComponent<MonsterAudio>();
        a.monster = monster;
        a.Build();
        return a;
    }

    void Build()
    {
        if (droneClip == null)
        {
            droneClip = FartAudio.MakeClip("MonsterDrone", RenderDrone());
            heartClip = FartAudio.MakeClip("Heartbeat", RenderHeartbeat());
            stingClip = FartAudio.MakeClip("MonsterSting", RenderSting());
            roarClip = FartAudio.MakeClip("MonsterRoar", RenderRoar());
            shriekClip = FartAudio.MakeClip("MonsterShriek", RenderShriek());
        }
        drone = Source("Drone", 1f, 3.5f, 48f);
        drone.clip = droneClip;
        drone.loop = true;
        drone.volume = 0f;
        voice = Source("Voice", 1f, 5f, 60f);
        growl = Source("Growl", 1f, 5f, 60f);
        flat = Source("Flat", 0f, 1f, 500f);
        heart = Source("Heart", 0f, 1f, 500f);
        heart.clip = heartClip;
    }

    AudioSource Source(string name, float spatial, float min, float max)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.up * 2.2f;
        var s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.dopplerLevel = 0f;
        s.spatialBlend = spatial;
        s.rolloffMode = AudioRolloffMode.Linear;
        s.minDistance = min;
        s.maxDistance = max;
        return s;
    }

    void Update()
    {
        var gm = GameManager.Instance;
        float dt = Time.deltaTime;
        var state = monster.State;
        bool present = monster.Visible;
        bool omen = state == FartMonster.Phase.Omen;

        // the drone: around you and faint while the lights fail, then it has a place
        float want = present ? 0.75f : omen ? 0.4f * Mathf.Clamp01(monster.StateTime / FartMonster.OmenSeconds) : 0f;
        drone.spatialBlend = present ? 1f : 0f;
        drone.pitch = omen ? 0.75f : 1f;
        drone.volume = Mathf.MoveTowards(drone.volume, want, dt * 0.9f);
        if (drone.volume > 0f && !drone.isPlaying) drone.Play();
        else if (drone.volume <= 0f && drone.isPlaying) drone.Stop();

        if (state == FartMonster.Phase.Hunting && Time.time >= nextGrowl)
        {
            Growl();
            nextGrowl = Time.time + Random.Range(3.5f, 6.5f);
        }

        // the player's heart
        var player = gm != null ? gm.Player : null;
        if (player == null || !present || gm.GameOver || player.Stunned) return;
        float dist = Vector3.Distance(player.transform.position, monster.transform.position + Vector3.up * 0.9f);
        float fear = Mathf.Clamp01(1f - (dist - 3f) / (monster.Target == player ? 24f : 11f));
        if (fear > 0.03f && Time.time >= nextBeat)
        {
            heart.volume = Mathf.Lerp(0.4f, 1f, fear);
            heart.pitch = Mathf.Lerp(0.92f, 1.12f, fear);
            heart.Play();
            nextBeat = Time.time + Mathf.Lerp(1.1f, 0.36f, fear);
        }
    }

    void Growl()
    {
        var clip = FartAudio.AnyLong();
        if (clip == null) return;
        growl.pitch = Random.Range(0.30f, 0.42f);
        growl.clip = clip;
        growl.volume = 1f;
        growl.Play();
    }

    /// <summary>It bursts out of the floor.</summary>
    public void Emerge(bool first)
    {
        flat.PlayOneShot(stingClip, first ? 1f : 0.75f);
        Growl();
        nextGrowl = Time.time + Random.Range(3f, 5f);
    }

    /// <summary>It has caught somebody; a caught player hears it inside their head.</summary>
    public void Roar(bool atPlayer)
    {
        if (atPlayer) flat.PlayOneShot(roarClip, 1f);
        else voice.PlayOneShot(roarClip, 1f);
        Growl();
    }

    /// <summary>A blast drove it off.</summary>
    public void Shriek() => voice.PlayOneShot(shriekClip, 1f);

    // ------------------------------------------------------------------ synthesis

    static float Saw(double phase) => (float)(2.0 * (phase - System.Math.Floor(phase)) - 1.0);

    static void Normalize(float[] buf, float peak)
    {
        float max = 1e-6f;
        foreach (float v in buf) max = Mathf.Max(max, Mathf.Abs(v));
        float g = peak / max;
        for (int i = 0; i < buf.Length; i++) buf[i] *= g;
    }

    /// <summary>
    /// Four seconds that loop without a seam: every partial completes a whole
    /// number of cycles in that time. The fundamental is felt on real speakers;
    /// the buzz of its harmonics and the breath above it carry on small ones.
    /// </summary>
    public static float[] RenderDrone()
    {
        const float seconds = 4f;
        int n = (int)(seconds * Rate);
        var buf = new float[n];
        var rng = new System.Random(77);
        float breath = 0f, breath2 = 0f;
        float kLo = 1f - Mathf.Exp(-2f * Mathf.PI * 260f / Rate), kHi = 1f - Mathf.Exp(-2f * Mathf.PI * 900f / Rate);
        int fade = Rate / 4;                        // extra noise, folded over the start so the loop has no seam
        var hiss = new float[n + fade];
        for (int i = 0; i < n + fade; i++)
        {
            float w = (float)rng.NextDouble() * 2f - 1f;
            breath += (w - breath) * kHi;           // below 900 Hz
            breath2 += (breath - breath2) * kLo;    // below 260 Hz
            hiss[i] = breath - breath2;             // the band between
        }
        for (int i = 0; i < fade; i++)
        {
            float u = i / (float)fade;
            hiss[i] = hiss[i] * u + hiss[n + i] * (1f - u);
        }
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)Rate;
            double tau = 2.0 * System.Math.PI * t;
            float slow = (float)System.Math.Sin(tau * 0.25);            // one swell per loop
            float x = 0.16f * (float)System.Math.Sin(tau * 55.0)
                    + 0.16f * (float)System.Math.Sin(tau * 82.5 + 0.8 * slow)
                    + 0.20f * (float)System.Math.Sin(tau * 110.25)
                    + 0.26f * (float)System.Math.Sin(tau * 165.0) * (0.6f + 0.4f * (float)System.Math.Sin(tau * 0.5))
                    + 0.20f * (float)System.Math.Sin(tau * 233.5 + 1.2 * slow);       // a sour interval on top
            float buzz = 0f;                                             // a rasp on the fundamental
            for (int h = 3; h <= 12; h++) buzz += (float)System.Math.Sin(tau * 55.0 * h + h) / Mathf.Sqrt(h);
            x += 0.20f * buzz * (0.55f + 0.45f * (float)System.Math.Sin(tau * 0.75 + 1.0));
            x += 3.2f * hiss[i] * (0.55f + 0.45f * slow);
            buf[i] = (float)System.Math.Tanh(x * 1.2f);
        }
        Normalize(buf, 0.8f);
        return buf;
    }

    /// <summary>Lub-dub. Each thump drops in pitch and carries overtones a phone can reproduce.</summary>
    public static float[] RenderHeartbeat()
    {
        int n = (int)(0.5f * Rate);
        var buf = new float[n];
        void Thump(float at, float amp)
        {
            int start = (int)(at * Rate);
            double phase = 0.0;
            for (int i = start; i < n; i++)
            {
                float t = (i - start) / (float)Rate;
                float f = Mathf.Lerp(170f, 78f, Mathf.Clamp01(t / 0.07f));
                phase += f / Rate;
                float env = Mathf.Exp(-t / 0.05f) * Mathf.Min(1f, t / 0.003f);
                double p = 2.0 * System.Math.PI * phase;
                buf[i] += amp * env * (float)(System.Math.Sin(p) + 0.8 * System.Math.Sin(2 * p) + 0.6 * System.Math.Sin(3 * p) + 0.35 * System.Math.Sin(5 * p));
            }
        }
        Thump(0f, 1f);
        Thump(0.19f, 0.72f);
        for (int i = 0; i < n; i++) buf[i] = (float)System.Math.Tanh(buf[i] * 1.4f);
        Normalize(buf, 0.9f);
        return buf;
    }

    /// <summary>A cluster of notes that grind against each other, hit hard and left to sag.</summary>
    public static float[] RenderSting()
    {
        int n = (int)(2.6f * Rate);
        var buf = new float[n];
        var rng = new System.Random(13);
        double[] notes = { 146.83, 155.56, 207.65, 293.66, 311.13, 415.30, 587.33, 622.25 };   // D, E flat, A flat: seconds and tritones
        var phase = new double[notes.Length];
        float lp = 0f, k = 1f - Mathf.Exp(-2f * Mathf.PI * 2600f / Rate);
        float crash = 0f, kc = 1f - Mathf.Exp(-2f * Mathf.PI * 3200f / Rate);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float bend = 1f - 0.045f * t;                                   // the whole chord sags
            float x = 0f;
            for (int v = 0; v < notes.Length; v++)
            {
                phase[v] += notes[v] * bend * (1.0 + 0.004 * System.Math.Sin(t * (5.0 + v))) / Rate;
                x += Saw(phase[v]) / (1f + v * 0.25f);
            }
            float trem = 1f - 0.3f * Mathf.Clamp01(t / 0.8f) * (0.5f + 0.5f * Mathf.Sin(t * 2f * Mathf.PI * 9f));
            float env = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t / 0.95f) * trem;
            crash += (((float)rng.NextDouble() * 2f - 1f) - crash) * kc;
            float y = x * 0.22f * env + crash * 1.6f * Mathf.Exp(-t / 0.06f)
                    + 0.8f * Mathf.Sin(2f * Mathf.PI * (95f - 40f * Mathf.Clamp01(t / 0.25f)) * t) * Mathf.Exp(-t / 0.22f);
            lp += (y - lp) * k;
            buf[i] = (float)System.Math.Tanh(lp * 1.6f);
        }
        int tail = Rate / 10;
        for (int i = 0; i < tail; i++) buf[n - 1 - i] *= i / (float)tail;
        Normalize(buf, 0.92f);
        return buf;
    }

    /// <summary>The fart to end all farts: low, ragged, breaking up.</summary>
    public static float[] RenderRoar()
    {
        var rng = new System.Random(666);
        var a = FartAudio.Synthesize(new FartAudio.Spec
        {
            duration = 2.4f, f0 = 96f, f1 = 52f, wobble = 0.22f, jitter = 0.14f, shimmer = 0.55f, sputter = 0.07f,
            noise = 0.6f, sharp = 3.2f, formant1 = 340f, formant2 = 980f, lowpass = 3600f, drive = 3.6f, putter = false,
        }, rng);
        var b = FartAudio.Synthesize(new FartAudio.Spec
        {
            duration = 2.4f, f0 = 190f, f1 = 120f, wobble = 0.3f, jitter = 0.2f, shimmer = 0.6f, sputter = 0.12f,
            noise = 0.75f, sharp = 2.4f, formant1 = 620f, formant2 = 1500f, lowpass = 4200f, drive = 3f, putter = false,
        }, rng);
        var buf = new float[a.Length];
        for (int i = 0; i < buf.Length; i++)
        {
            float t = i / (float)Rate;
            float swell = Mathf.Min(1f, t / 0.05f) * (0.75f + 0.25f * Mathf.Sin(t * 2f * Mathf.PI * 6.5f));
            buf[i] = (float)System.Math.Tanh((a[i] + 0.55f * b[Mathf.Min(i, b.Length - 1)]) * swell * 1.3f);
        }
        Normalize(buf, 0.95f);
        return buf;
    }

    /// <summary>A long squeal falling away as it comes apart.</summary>
    public static float[] RenderShriek()
    {
        var rng = new System.Random(31);
        var buf = FartAudio.Synthesize(new FartAudio.Spec
        {
            duration = 1.1f, f0 = 560f, f1 = 150f, wobble = 0.12f, jitter = 0.07f, shimmer = 0.3f, sputter = 0.02f,
            noise = 0.4f, sharp = 4.5f, formant1 = 640f, formant2 = 1900f, lowpass = 5000f, drive = 2.2f, putter = true,
        }, rng);
        Normalize(buf, 0.9f);
        return buf;
    }
}

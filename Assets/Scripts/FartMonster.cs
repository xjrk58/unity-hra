using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The fart monster ("prdová příšera"). About 45 seconds into a match the
/// lights start to fail, the day turns into a green gloom and the monster boils
/// up out of the floor behind somebody and hunts them:
///
///   Dormant → Omen (lights flicker) → Emerging → Hunting → Feeding → Dissolving → Lurking → Emerging …
///
/// It is made of gas, so it has no collider and drifts straight through
/// columns, railings and props; it speeds up the longer a hunt lasts (a walk
/// will not save you, a sprint will). If its prey leaves the deck it sinks
/// through the floor and comes up near them. Whoever it catches loses the whole
/// backpack and is knocked out; a caught player gets it in the face first. A
/// fart blast (same price as blasting a rival) drives it off for a while.
///
/// This class is the monster's mind and position only; <see cref="MonsterVisual"/>
/// and <see cref="MonsterAudio"/> show and sound whatever state it is in.
///
/// In a two-player game the host's monster does the thinking. On the joining
/// device the monster is a puppet: it takes its state from the host
/// (<see cref="NetRead"/>) and only does the showing - the announcements, the
/// gloom, the sounds, and the scare if this device's player is the one caught.
/// </summary>
public class FartMonster : MonoBehaviour
{
    public enum Phase { Dormant, Omen, Emerging, Hunting, Feeding, Dissolving, Lurking }

    public static FartMonster Instance { get; private set; }

    /// <summary>Tests that are about something else switch the monster off.</summary>
    public static bool Enabled = true;

    /// <summary>Seconds after the match starts when the monster first appears.</summary>
    public static float FirstAppearance = 45f;

    public const float OmenSeconds = 7f;        // the lights fail this long before it appears
    public const float EmergeSeconds = 1.8f;
    public const float DissolveSeconds = 1.1f;
    public const float ScareSeconds = 1.35f;    // a caught player stares into its face this long
    public const float StunSeconds = 5f;

    const float CatchRadius = 1.5f;

    /// <summary>How far from its victim it rears up to roar: far enough for the whole face to be seen.</summary>
    public const float CatchDistance = 3.3f;
    const float BaseSpeed = 4.9f, TopSpeed = 6.6f, SpeedGain = 0.13f;   // bots walk 4.7, the player 6, sprint 8.6
    const float MaxHunt = 28f;
    const float BodyRadius = 0.95f;

    static readonly Color Red = new Color(1f, 0.16f, 0.08f);

    public Phase State { get; private set; } = Phase.Dormant;
    public float StateTime { get; private set; }

    /// <summary>Who it is after (null when nobody).</summary>
    public Competitor Target { get; private set; }

    /// <summary>Who it caught last.</summary>
    public Competitor Victim { get; private set; }

    /// <summary>How many times it has come out of the floor this match.</summary>
    public int Appearances { get; private set; }

    /// <summary>True while it is in the world to be seen.</summary>
    public bool Visible => State == Phase.Emerging || State == Phase.Hunting || State == Phase.Feeding || State == Phase.Dissolving;

    /// <summary>True while a fart blast can drive it off.</summary>
    public bool CanBeBlasted => State == Phase.Emerging || State == Phase.Hunting;

    public Vector3 ChestPosition => transform.position + Vector3.up * 2.1f;

    /// <summary>Current speed over the ground (for the animation).</summary>
    public float Speed { get; private set; }

    public MonsterVisual Visual { get; private set; }
    public MonsterAudio Sound { get; private set; }

    float lurkFor, feedFor, huntSpeed, offDeck, dread, blackout;
    bool keepTarget, finished, moodApplied;
    Competitor scared;          // a caught person: knocked out only after the scare
    int repelSeq;
    bool puppet;
    Vector3 netPosition;
    Quaternion netRotation = Quaternion.identity;

    /// <summary>Called once per scene by the bootstrap (does nothing when switched off).</summary>
    public static FartMonster Build(bool puppet = false)
    {
        Instance = null;
        if (!Enabled) return null;
        FirstAppearance = Session.FlagValue("-monsterAt", FirstAppearance);    // (dev tools can hurry it up)
        var go = new GameObject("FartMonster");
        Instance = go.AddComponent<FartMonster>();
        Instance.puppet = puppet;
        Instance.Visual = MonsterVisual.Attach(Instance);
        Instance.Sound = MonsterAudio.Attach(Instance);
        return Instance;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Go(Phase next)
    {
        State = next;
        StateTime = 0f;
        if (next == Phase.Dissolving) ShowDissolve();
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        float dt = Time.deltaTime;
        StateTime += dt;

        if (puppet)
        {
            // the host does the thinking: glide after what it reports and keep up the gloom
            float k = 1f - Mathf.Exp(-14f * dt);
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position, netPosition, k), Quaternion.Slerp(transform.rotation, netRotation, k));
            UpdateMood(dt);
            return;
        }
        Speed = 0f;

        if (gm.GameOver && !finished)
        {
            finished = true;                    // the match is over: it melts away for good
            scared = null;
            if (Visible && State != Phase.Dissolving) Go(Phase.Dissolving);
            else if (!Visible) Go(Phase.Dormant);
        }

        switch (State)
        {
            case Phase.Dormant:
                if (!finished && gm.MatchTime >= FirstAppearance - OmenSeconds)
                {
                    Go(Phase.Omen);
                    ShowOmen();
                }
                break;

            case Phase.Omen:
                if (gm.MatchTime >= FirstAppearance) Emerge(PickTarget());
                break;

            case Phase.Emerging:
                Face(Target, 720f * dt);
                if (StateTime >= EmergeSeconds) Go(Phase.Hunting);
                break;

            case Phase.Hunting:
                Hunt(dt);
                break;

            case Phase.Feeding:
                Face(Victim, 720f * dt);
                if (scared != null && StateTime >= ScareSeconds)
                {
                    scared.Stun(StunSeconds, "monster");
                    scared = null;
                }
                if (StateTime >= feedFor)
                {
                    lurkFor = Random.Range(12f, 17f);
                    keepTarget = false;
                    Go(Phase.Dissolving);
                }
                break;

            case Phase.Dissolving:
                if (StateTime >= DissolveSeconds) Go(finished ? Phase.Dormant : Phase.Lurking);
                break;

            case Phase.Lurking:
                if (finished) Go(Phase.Dormant);
                else if (StateTime >= lurkFor) Emerge(keepTarget && Huntable(Target) ? Target : PickTarget());
                break;
        }

        UpdateMood(dt);
    }

    // ------------------------------------------------------------------ the hunt

    static bool Huntable(Competitor c) => c != null && !c.Stunned && !c.Gone;

    static Vector3 Feet(Competitor c) => c.transform.position - Vector3.up * 0.9f;

    // People are tastier than bots, and it does not go straight back for whoever it just ate.
    Competitor PickTarget()
    {
        var gm = GameManager.Instance;
        float total = 0f;
        var pool = new List<Competitor>();
        foreach (var c in gm.Competitors)
            if (Huntable(c) && c != Victim) pool.Add(c);
        if (pool.Count == 0)
            foreach (var c in gm.Competitors)
                if (Huntable(c)) pool.Add(c);
        foreach (var c in pool) total += c.IsHuman ? 3f : 1f;
        float pick = Random.value * total;
        foreach (var c in pool)
        {
            pick -= c.IsHuman ? 3f : 1f;
            if (pick <= 0f) return c;
        }
        return pool.Count > 0 ? pool[pool.Count - 1] : null;
    }

    void Emerge(Competitor target)
    {
        if (target == null)
        {
            lurkFor = 2f;           // nobody to hunt right now: wait in the floor
            keepTarget = false;
            Go(Phase.Lurking);
            return;
        }
        Target = target;
        keepTarget = false;
        offDeck = 0f;
        huntSpeed = BaseSpeed;
        transform.position = PickSpot(target);
        Face(target, 360f);
        Appearances++;
        Go(Phase.Emerging);
        ShowEmerge();
    }

    // ------------------------------------------------------------------ what everybody sees and hears
    // (run by the monster that decides and, on a joining device, by its puppet)

    void ShowOmen()
    {
        if (Hud.Instance != null) Hud.Instance.Announce("Něco se blíží…", "", new Color(0.8f, 0.95f, 0.6f), 4f);
    }

    void ShowEmerge()
    {
        GasFx.MonsterBurst(transform.position + Vector3.up * 0.3f);
        blackout = 0.5f;            // every lamp dies for a moment
        if (Sound != null) Sound.Emerge(Appearances <= 1);
        if (Hud.Instance != null && Target != null)
            Hud.Instance.Announce(Appearances <= 1 ? "PRDOVÁ PŘÍŠERA!" : "PŘÍŠERA JE ZPĚT!",
                Target.IsPlayer ? "Jde po tobě! Uteč – nebo ji zažeň prdem." : $"Loví: {Target.DisplayName}", Red, 3.4f);
    }

    void ShowDissolve()
    {
        GasFx.MonsterBurst(transform.position + Vector3.up * 0.8f);
    }

    void ShowCatch()
    {
        if (Victim == null) return;
        if (Sound != null) Sound.Roar(Victim.IsPlayer);
        if (Victim is PlayerController player)
        {
            // this device's player gets it in the face
            Debug.Log("[Monster] caught this device's player");
            player.Scare(Visual != null ? Visual.Face : transform, ScareSeconds);
            if (Hud.Instance != null) Hud.Instance.Flash(new Color(0.75f, 1f, 0.55f), 0.55f);
#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
        }
    }

    void ShowRepel()
    {
        if (Sound != null) Sound.Shriek();
        if (Hud.Instance != null) Hud.Instance.Announce("Příšera zahnána!", "", new Color(0.6f, 1f, 0.35f), 2.2f);
    }

    // Somewhere on the prey's deck, 10–14 m away, on solid floor and preferably behind its back.
    Vector3 PickSpot(Competitor target)
    {
        Vector3 feet = Feet(target);
        int deck = Arena.FloorOfFeet(feet.y);
        Vector3 fwd = target.transform.forward;
        float limit = Arena.Half - 1.6f;
        Vector3 best = new Vector3(-Mathf.Sign(feet.x + 0.01f) * (limit - 1f), Arena.FloorY(deck), -Mathf.Sign(feet.z + 0.01f) * (limit - 1f));
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < 48; i++)
        {
            float a = Random.value * Mathf.PI * 2f, r = Random.Range(10f, 14f);
            float x = feet.x + Mathf.Cos(a) * r, z = feet.z + Mathf.Sin(a) * r;
            if (Mathf.Abs(x) > limit || Mathf.Abs(z) > limit) continue;
            if (Arena.InHole(deck, x, z, 1.6f) || Arena.OnRampFootprint(deck, x, z, 1.2f)) continue;
            Vector3 dir = new Vector3(x - feet.x, 0f, z - feet.z).normalized;
            float score = -Vector3.Dot(dir, fwd) * 2f + Random.value;      // behind is best
            if (score > bestScore) { bestScore = score; best = new Vector3(x, Arena.FloorY(deck), z); }
        }
        return best;
    }

    void Hunt(float dt)
    {
        if (!Huntable(Target))
        {
            Target = PickTarget();
            if (Target == null)
            {
                lurkFor = 6f;
                Go(Phase.Dissolving);
                return;
            }
        }

        Vector3 me = transform.position, feet = Feet(Target);
        Vector3 flat = new Vector3(feet.x - me.x, 0f, feet.z - me.z);
        float dist = flat.magnitude, dy = feet.y - me.y;

        // the prey got away to another deck: sink through the floor and come up near it
        offDeck = Mathf.Abs(dy) > 2.6f ? offDeck + dt : 0f;
        if (offDeck > 1.5f)
        {
            keepTarget = true;
            lurkFor = 2.2f;
            Go(Phase.Dissolving);
            return;
        }
        if (StateTime > MaxHunt)
        {
            keepTarget = false;
            lurkFor = Random.Range(9f, 14f);
            Go(Phase.Dissolving);
            return;
        }

        huntSpeed = Mathf.Min(TopSpeed, huntSpeed + SpeedGain * dt);
        Speed = huntSpeed;
        if (dist > 0.01f)
        {
            Vector3 dir = flat / dist;
            me += dir * Mathf.Min(huntSpeed * dt, dist);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), 300f * dt);
        }
        if (Mathf.Abs(dy) <= 2.6f) me.y = Mathf.MoveTowards(me.y, feet.y, 2.6f * dt);     // up and down ramps after it
        float limit = Arena.Half - 0.8f;
        me.x = Mathf.Clamp(me.x, -limit, limit);
        me.z = Mathf.Clamp(me.z, -limit, limit);
        transform.position = me;

        if (dist < CatchRadius && Mathf.Abs(dy) < 1.6f) Catch(Target);
    }

    void Face(Competitor c, float maxDegrees)
    {
        if (c == null) return;
        Vector3 to = c.transform.position - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), maxDegrees);
    }

    void Catch(Competitor victim)
    {
        Victim = victim;
        victim.LoseBackpack();
        Go(Phase.Feeding);

        // loom over the victim from just out of arm's reach
        Vector3 feet = Feet(victim);
        Vector3 away = transform.position - feet;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = -victim.transform.forward;
        transform.position = new Vector3(feet.x, transform.position.y, feet.z) + away.normalized * CatchDistance;
        Face(victim, 360f);

        if (victim.IsHuman)
        {
            // a person gets it in the face first (on their own device), then goes down
            feedFor = ScareSeconds + 1.0f;
            scared = victim;
        }
        else
        {
            feedFor = 1.4f;
            victim.Stun(StunSeconds, "monster");
        }
        ShowCatch();
    }

    /// <summary>Hit by a fart blast: it shrieks, falls apart and stays away for a while.</summary>
    public void Repel()
    {
        if (!CanBeBlasted) return;
        keepTarget = false;
        lurkFor = Random.Range(14f, 18f);
        repelSeq = (repelSeq + 1) & 0xff;
        Go(Phase.Dissolving);
        ShowRepel();
    }

    // ------------------------------------------------------------------ two devices

    static Competitor At(int index)
    {
        var gm = GameManager.Instance;
        return gm != null && index >= 0 && index < gm.Competitors.Count ? gm.Competitors[index] : null;
    }

    /// <summary>Host: everything the joining device needs to show the monster.</summary>
    public void NetWrite(System.IO.BinaryWriter w)
    {
        var gm = GameManager.Instance;
        Vector3 p = transform.position;
        w.Write((byte)State);
        w.Write(StateTime);
        w.Write(p.x); w.Write(p.y); w.Write(p.z);
        w.Write(transform.eulerAngles.y);
        w.Write((sbyte)(Target != null ? gm.IndexOf(Target) : -1));
        w.Write((sbyte)(Victim != null ? gm.IndexOf(Victim) : -1));
        w.Write((byte)Appearances);
        w.Write((byte)repelSeq);
        w.Write(Speed);
    }

    /// <summary>Joining device: take the host's word for where the monster is and what it is doing.</summary>
    public void NetRead(System.IO.BinaryReader r)
    {
        var phase = (Phase)r.ReadByte();
        float stateTime = r.ReadSingle();
        Vector3 p = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        float yaw = r.ReadSingle();
        Target = At(r.ReadSByte());
        Victim = At(r.ReadSByte());
        Appearances = r.ReadByte();
        int repels = r.ReadByte();
        Speed = r.ReadSingle();

        netPosition = p;
        netRotation = Quaternion.Euler(0f, yaw, 0f);
        bool jump = phase != State && (phase == Phase.Emerging || phase == Phase.Feeding);
        if (jump || !Visible || (p - transform.position).sqrMagnitude > 25f)
            transform.SetPositionAndRotation(netPosition, netRotation);        // it came up somewhere else: no gliding

        if (repels != repelSeq)
        {
            repelSeq = repels;
            ShowRepel();
        }
        if (phase != State)
        {
            State = phase;
            switch (phase)
            {
                case Phase.Omen: ShowOmen(); break;
                case Phase.Emerging: ShowEmerge(); break;
                case Phase.Feeding: ShowCatch(); break;
                case Phase.Dissolving: ShowDissolve(); break;
            }
        }
        StateTime = stateTime;
        finished = GameManager.Instance != null && GameManager.Instance.GameOver;
    }

    /// <summary>Joining device: the host has no monster.</summary>
    public void NetGone()
    {
        State = Phase.Dormant;
    }

    /// <summary>True if the ray passes through the monster's body within <paramref name="reach"/>.</summary>
    public bool RayHits(Ray ray, float reach)
    {
        if (!Visible) return false;
        for (int i = 0; i < 6; i++)
        {
            Vector3 c = transform.position + Vector3.up * (0.5f + i * 0.52f);
            float t = Vector3.Dot(c - ray.origin, ray.direction);
            if (t <= 0f || t >= reach) continue;
            if ((c - ray.GetPoint(t)).sqrMagnitude <= BodyRadius * BodyRadius) return true;
        }
        return false;
    }

    // ------------------------------------------------------------------ the gloom

    // While the monster is out it is night; between its visits a murky dusk. The ceiling
    // lamps sag and stutter with it, and die completely for a moment when it appears.
    void UpdateMood(float dt)
    {
        float want;
        switch (State)
        {
            case Phase.Omen: want = 0.6f * Mathf.Clamp01(StateTime / OmenSeconds); break;
            case Phase.Emerging:
            case Phase.Hunting:
            case Phase.Feeding: want = 1f; break;
            case Phase.Dissolving: want = finished ? 0f : 0.7f; break;
            case Phase.Lurking: want = 0.45f; break;
            default: want = 0f; break;
        }
        dread = Mathf.MoveTowards(dread, want, dt * (want > dread ? 1.1f : 0.28f));
        if (blackout > 0f) blackout -= dt;
        if (dread <= 0f && blackout <= 0f && !moodApplied) return;       // plain daylight, nothing to do

        float lamps = Mathf.Lerp(1f, 0.36f, dread);
        if (dread > 0.03f)
        {
            float n = Mathf.PerlinNoise(Time.time * 11f, 3.7f);
            if (n > 0.66f) lamps *= 0.25f;                               // stutter
            else if (n < 0.22f) lamps *= 1.3f;
        }
        if (blackout > 0f) lamps = 0f;
        if (LightingRig.Instance != null) LightingRig.Instance.SetMood(dread, lamps);
        moodApplied = dread > 0f || blackout > 0f;
    }

    // ------------------------------------------------------------------ for the dev tools

    /// <summary>
    /// Puts the monster somewhere in a given state at once, gloom and all
    /// (screenshots and tests; never used by the game itself).
    /// </summary>
    public void Summon(Vector3 position, Competitor target, Phase phase, float stateTime = 0f)
    {
        transform.position = position;
        Target = target;
        if (phase == Phase.Feeding) Victim = target;
        Face(target, 360f);
        State = phase;
        StateTime = stateTime;
        feedFor = 60f;
        huntSpeed = BaseSpeed;
        offDeck = 0f;
        dread = Visible ? 1f : 0f;
        if (LightingRig.Instance != null) LightingRig.Instance.SetMood(dread, Visible ? 0.36f : 1f);
        moodApplied = Visible;
        if (Visual != null) Visual.Settle();
    }
}

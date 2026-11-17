using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A horse or a unicorn that wanders about one deck of the car park and, every
/// so often, stops, lifts its tail and farts. A horse leaves an ordinary green
/// fart behind; a unicorn farts a rainbow and leaves a rainbow fart, which is
/// worth three.
///
/// Each animal owns exactly one collectable fart, made when the world is built
/// and moved to wherever the animal last farted - the set of farts in a match
/// never changes, which is what lets two devices refer to them by number.
///
/// The animals are scenery with a purpose: they have no collider (nobody gets
/// stuck on a horse), stay on their deck and walk round columns and ramps with
/// the same planner the bots use. They are shy: when a player or a bot comes
/// close they hurry out of the way, so nobody ends up standing inside a horse. In a two-player match the host's animals
/// decide; the joining device shows them, and their farts, where the host says
/// they are.
/// </summary>
public class FartAnimal : MonoBehaviour
{
    /// <summary>Every animal in the match, in the same order on both devices.</summary>
    public static readonly List<FartAnimal> All = new List<FartAnimal>();

    /// <summary>Tests that are about something else leave the animals out.</summary>
    public static bool Enabled = true;

    const float FartSeconds = 1.1f;
    const float RearOffset = 1.15f, RearHeight = 1.05f;     // where the tail is, from the middle of the animal
    const float HorseDrop = 1.6f;                           // a horse's fart hangs this far behind its middle
    const float RainbowReach = 3.0f;                        // a rainbow comes down this far behind the unicorn's rear
    const float EdgeLimit = Arena.Half - 1.9f;              // (the barrels and crates stand along the edges)
    const float ShyDistance = 2.6f, ShySpeed = 7f;          // (the player walks at 6 m/s and sprints at 8.6)

    public bool unicorn;
    public int deck;
    public FartPickup pickup;

    /// <summary>Counts this animal's farts (how the other device learns of one).</summary>
    public int FartSeq { get; private set; }

    /// <summary>Speed over the ground, for the legs.</summary>
    public float Speed { get; private set; }

    /// <summary>Seconds of the current fart left (0 when not farting).</summary>
    public float FartLeft { get; private set; }

    /// <summary>True while it stands with its head down.</summary>
    public bool Grazing { get; private set; }

    bool puppet, pinned;
    Vector3 netPosition, target, waypoint, stuckAt;
    Quaternion netRotation = Quaternion.identity;
    float pause, nextFart, replan, stuckFor, shy;

    /// <summary>Makes one animal (and the fart it will leave) on a deck.</summary>
    public static FartAnimal Spawn(bool unicorn, int deck, Color coat, Transform pickups, bool puppet)
    {
        var go = new GameObject(unicorn ? "Unicorn" : "Horse");
        var a = go.AddComponent<FartAnimal>();
        a.unicorn = unicorn;
        a.deck = deck;
        a.puppet = puppet;
        go.transform.SetPositionAndRotation(RandomSpot(deck, 7f), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        a.netPosition = go.transform.position;
        a.netRotation = go.transform.rotation;
        a.target = a.waypoint = go.transform.position;
        a.nextFart = Random.Range(4f, 9f);
        a.pause = Random.Range(0.5f, 2.5f);

        a.pickup = GameBootstrap.MakePickup(go.transform.position + Vector3.up * 0.75f, pickups);
        a.pickup.MakeAnimalDrop(unicorn);

        go.AddComponent<AnimalVisual>().Build(a, coat);
        All.Add(a);
        return a;
    }

    void OnDestroy()
    {
        All.Remove(this);
    }

    // A free spot on a deck: not in a hole, on a ramp, in a column or in the goal - and,
    // when asked, at least so far from everybody (nobody starts a match behind a horse).
    static Vector3 RandomSpot(int deck, float clearOfPeople = 0f)
    {
        for (int i = 0; i < 200; i++)
        {
            float x = Random.Range(-10.5f, 10.5f), z = Random.Range(-10.5f, 10.5f);
            if (Arena.InHole(deck, x, z, 1.8f) || Arena.OnRampFootprint(deck, x, z, 1.6f)) continue;
            if (clearOfPeople > 0f && i < 150 && Somebody(deck, new Vector3(x, 0f, z), clearOfPeople) != null) continue;
            bool column = false;
            foreach (float gx in Arena.Grid)
                foreach (float gz in Arena.Grid)
                    if (Mathf.Abs(x - gx) < 1.6f && Mathf.Abs(z - gz) < 1.6f) column = true;
            if (column) continue;
            if (deck == 0 && Vector2.Distance(new Vector2(x, z), new Vector2(Arena.GoalPos.x, Arena.GoalPos.z)) < 5f) continue;
            return new Vector3(x, Arena.FloorY(deck), z);
        }
        return new Vector3(-8f, Arena.FloorY(deck), -8f);
    }

    // The nearest player or bot on this deck within so many metres of a spot, if any.
    static Competitor Somebody(int deck, Vector3 spot, float within)
    {
        var gm = GameManager.Instance;
        if (gm == null) return null;
        Competitor nearest = null;
        float best = within * within;
        foreach (var c in gm.Competitors)
        {
            if (c == null || c.Gone) continue;
            Vector3 d = c.transform.position - spot;
            if (Mathf.Abs(c.transform.position.y - 0.95f - Arena.FloorY(deck)) > 2.5f) continue;     // on another deck, or well up a ramp
            d.y = 0f;
            if (d.sqrMagnitude < best) { best = d.sqrMagnitude; nearest = c; }
        }
        return nearest;
    }

    // Somebody is too close: the place to hurry to. Straight away from them if there is
    // room, otherwise off to one side (an animal in a corner slips out along the wall).
    Vector3 Refuge(Vector3 pos, Vector3 from)
    {
        Vector3 away = pos - from;
        away.y = 0f;
        away = away.sqrMagnitude > 0.01f ? away.normalized : transform.right;
        float limit = EdgeLimit - 0.3f, bestScore = -1f;
        Vector3 best = pos;
        foreach (float angle in RefugeAngles)
        {
            Vector3 to = pos + Quaternion.Euler(0f, angle, 0f) * away * 4.5f;
            to.x = Mathf.Clamp(to.x, -limit, limit);
            to.z = Mathf.Clamp(to.z, -limit, limit);
            if (Blocked(deck, to.x, to.z)) continue;
            Vector3 gap = to - from;
            gap.y = 0f;
            float score = gap.magnitude - Mathf.Abs(angle) * 0.004f;      // (straight away wins a tie)
            if (score > bestScore) { bestScore = score; best = to; }
        }
        return best;
    }

    static readonly float[] RefugeAngles = { 0f, 50f, -50f, 100f, -100f };

    void Update()
    {
        float dt = Time.deltaTime;
        if (FartLeft > 0f) FartLeft -= dt;

        if (puppet)
        {
            float k = 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime);
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position, netPosition, k), Quaternion.Slerp(transform.rotation, netRotation, k));
            return;
        }

        var gm = GameManager.Instance;
        Speed = 0f;
        if (gm == null || gm.Paused || FartLeft > 0f || pinned) return;

        // shy: somebody has come close, so it hurries out of their way
        var crowd = Somebody(deck, transform.position, ShyDistance);
        if (crowd != null)
        {
            target = Refuge(transform.position, crowd.transform.position);
            pause = 0f;
            Grazing = false;
            shy = 0.5f;
            replan = 0f;
        }
        bool hurry = shy > 0f;
        if (hurry) shy -= dt;

        if (pause > 0f)
        {
            pause -= dt;
            if (pause <= 0f)
            {
                Grazing = false;
                target = RandomSpot(deck);
                replan = 0f;
                stuckFor = 0f;
                stuckAt = transform.position;
            }
            return;
        }

        nextFart -= dt;
        if (nextFart <= 0f && !hurry)       // (it does not stop to fart with somebody at its heels)
        {
            nextFart = Random.Range(7f, 12f);
            Fart();
            return;
        }

        Vector3 pos = transform.position;
        Vector3 toTarget = target - pos;
        toTarget.y = 0f;
        if (toTarget.magnitude < 0.9f)
        {
            pause = Random.Range(1.5f, 4.5f);       // arrived: stand, maybe graze
            Grazing = Random.value < 0.6f;
            return;
        }

        replan -= dt;
        if (replan <= 0f)
        {
            replan = 0.3f;
            waypoint = NavPlanner.Next(deck, pos, target);
        }
        Vector3 flat = waypoint - pos;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.0004f) flat = toTarget;
        Vector3 dir = flat.normalized;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), (hurry ? 480f : 200f) * dt);
        if (hurry)
        {
            Speed = ShySpeed;               // it does not turn round first: it jumps aside, then turns
            pos += dir * Speed * dt;
        }
        else
        {
            float facing = Mathf.Clamp01(Vector3.Dot(transform.forward, dir) * 1.5f - 0.3f);
            Speed = (unicorn ? 3.2f : 2.6f) * facing;
            pos += transform.forward * Speed * dt;
        }
        pos.x = Mathf.Clamp(pos.x, -EdgeLimit, EdgeLimit);
        pos.z = Mathf.Clamp(pos.z, -EdgeLimit, EdgeLimit);
        pos.y = Arena.FloorY(deck);
        transform.position = pos;

        // going nowhere (a corner, a railing): think of somewhere else
        stuckFor += dt;
        if (stuckFor > 3f)
        {
            if ((pos - stuckAt).sqrMagnitude < 1f) target = RandomSpot(deck);
            stuckFor = 0f;
            stuckAt = pos;
        }
    }

    /// <summary>Makes it fart on the spot (dev tools: screenshots and tests).</summary>
    public void FartNow() => Fart();

    /// <summary>
    /// Stands it somewhere, facing a way, and keeps it there (dev tools): it
    /// neither wanders nor shies until <see cref="Release"/>.
    /// </summary>
    public void Place(Vector3 at, float yaw)
    {
        at.y = Arena.FloorY(deck);
        transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
        netPosition = at;
        netRotation = transform.rotation;
        target = waypoint = at;
        pinned = true;
        pause = 0.5f;
        shy = 0f;
        Grazing = false;
        Speed = 0f;
        FartLeft = 0f;
    }

    /// <summary>Lets an animal stood somewhere with <see cref="Place"/> go its own way again.</summary>
    public void Release() => pinned = false;

    void Fart()
    {
        FartSeq = (FartSeq + 1) & 0xff;
        ShowFart(transform.position, transform.rotation);
        if (Landing(transform.position, transform.rotation, out Vector3 at))
            pickup.Drop(at + Vector3.up * 0.75f);       // (no free floor behind it: nothing to collect this time)
    }

    // Where a fart let go by an animal standing like this comes down: on the floor behind
    // it - a horse's right behind, a unicorn's at the far end of its rainbow - but never in
    // a hole, on a ramp, in a column or among the barrels at the edge. False if there is no
    // such spot (at is then the nearest try).
    bool Landing(Vector3 pos, Quaternion rot, out Vector3 at)
    {
        Vector3 back = -(rot * Vector3.forward);
        float reach = unicorn ? RearOffset + RainbowReach : HorseDrop;
        float limit = Arena.Half - 1.6f;
        at = pos;
        for (int i = 0; i < 3; i++, reach -= 0.9f)
        {
            at = pos + back * reach;
            at.x = Mathf.Clamp(at.x, -limit, limit);
            at.z = Mathf.Clamp(at.z, -limit, limit);
            at.y = Arena.FloorY(deck);
            if (!Blocked(deck, at.x, at.z)) return true;
        }
        return false;
    }

    static bool Blocked(int deck, float x, float z)
    {
        if (Arena.InHole(deck, x, z, 0.8f) || Arena.OnRampFootprint(deck, x, z, 0.5f)) return true;
        foreach (float gx in Arena.Grid)
            foreach (float gz in Arena.Grid)
                if (Mathf.Abs(x - gx) < 0.9f && Mathf.Abs(z - gz) < 0.9f) return true;
        return false;
    }

    // What everybody sees and hears (also run on a joining device).
    void ShowFart(Vector3 pos, Quaternion rot)
    {
        FartLeft = FartSeconds;
        Vector3 back = -(rot * Vector3.forward);
        Vector3 rear = pos + back * RearOffset + Vector3.up * RearHeight;
        if (unicorn)
        {
            Landing(pos, rot, out Vector3 land);
            GasFx.RainbowFart(rear, land, back);
            FartAudio.Animal(rear, true);
        }
        else
        {
            GasFx.Burst(rear, new Color(0.45f, 0.5f, 0.15f), 2.2f);
            FartAudio.Animal(rear, false);
        }
    }

    // ------------------------------------------------------------------ two devices

    public void NetWrite(System.IO.BinaryWriter w)
    {
        Vector3 p = transform.position;
        w.Write(p.x); w.Write(p.y); w.Write(p.z);
        w.Write(transform.eulerAngles.y);
        w.Write((byte)FartSeq);
        w.Write((byte)Mathf.Clamp(Mathf.RoundToInt(Speed * 20f), 0, 255));
        w.Write((byte)(Grazing ? 1 : 0));
        Vector3 h = pickup.Home;                    // where its fart hangs (the host alone decides that)
        w.Write(h.x); w.Write(h.y); w.Write(h.z);
    }

    public void NetRead(System.IO.BinaryReader r)
    {
        Vector3 p = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        float yaw = r.ReadSingle();
        int seq = r.ReadByte();
        Speed = r.ReadByte() / 20f;
        Grazing = r.ReadByte() != 0;
        Vector3 h = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        if ((p - transform.position).sqrMagnitude > 16f) transform.SetPositionAndRotation(p, Quaternion.Euler(0f, yaw, 0f));
        netPosition = p;
        netRotation = Quaternion.Euler(0f, yaw, 0f);
        pickup.NetMove(h);      // (whether it is there to be taken comes with the other farts)
        if (seq != FartSeq)
        {
            FartSeq = seq;
            ShowFart(p, netRotation);
        }
    }
}

/// <summary>
/// The body of a horse or unicorn: the model from Resources/Models/Horse
/// (Tools/blender/build_horse.py) and its animation. Like the monster it has no
/// skeleton: the head, the four legs and the tail are separate parts turned
/// about their joints. It trots with diagonal pairs of legs, nods, grazes,
/// swishes its tail - and lifts it, with a little hop, when it farts.
/// </summary>
public class AnimalVisual : MonoBehaviour
{
    FartAnimal animal;
    Transform model, head, tail, legFL, legFR, legBL, legBR;
    Quaternion restHead, restTail, restFL, restFR, restBL, restBR;
    float phase, headPitch, tailLift, seed;

    public void Build(FartAnimal owner, Color coat)
    {
        animal = owner;
        seed = Random.value * 10f;
        bool unicorn = owner.unicorn;

        // every animal gets its own coat; the unicorn's mane and hooves are its own colours too
        var body = new Material(Mats.Get("HorseBody")) { color = coat };
        var mane = new Material(Mats.Get("HorseMane"));
        var hoof = new Material(Mats.Get("HorseHoof"));
        if (unicorn)
        {
            mane.color = new Color(0.95f, 0.30f, 0.72f);
            hoof.color = new Color(0.90f, 0.72f, 0.22f);
        }
        else mane.color = Color.Lerp(coat, Color.black, 0.75f);

        model = ArenaBuilder.Spawn("Horse", transform.position, transform.rotation, transform, slot =>
        {
            switch (slot)
            {
                case "HorseMane": return mane;
                case "HorseHoof": return hoof;
                case "HorseEye": return Mats.Get("HorseEye");
                case "HorseHorn": return Mats.Get("HorseHorn");
                default: return body;
            }
        }).transform;

        Transform horn = null;
        foreach (var t in model.GetComponentsInChildren<Transform>(true))
        {
            switch (t.name)
            {
                case "HorseHead": head = t; break;
                case "HorseTail": tail = t; break;
                case "HorseLegFL": legFL = t; break;
                case "HorseLegFR": legFR = t; break;
                case "HorseLegBL": legBL = t; break;
                case "HorseLegBR": legBR = t; break;
                case "HorseHorn": horn = t; break;
            }
        }
        if (horn != null)
        {
            horn.gameObject.SetActive(unicorn);
            if (head != null) horn.SetParent(head, true);       // the horn goes where the head goes
        }
        if (head != null) restHead = head.localRotation;
        if (tail != null) restTail = tail.localRotation;
        if (legFL != null) restFL = legFL.localRotation;
        if (legFR != null) restFR = legFR.localRotation;
        if (legBL != null) restBL = legBL.localRotation;
        if (legBR != null) restBR = legBR.localRotation;
    }

    void LateUpdate() => Pose(Time.deltaTime);

    /// <summary>Jumps straight to the pose of the moment (dev tools).</summary>
    public void Settle()
    {
        for (int i = 0; i < 8; i++) Pose(0.25f);
    }

    void Pose(float dt)
    {
        if (model == null) return;
        float t = Time.time + seed;
        float go = Mathf.Clamp01(animal.Speed / 3f);
        phase += animal.Speed * 2.4f * dt;

        // the fart: a hop off the hind legs with the tail straight up
        float fart = animal.FartLeft > 0f ? Mathf.Clamp01(animal.FartLeft / 1.1f) : 0f;      // 1 at the start, 0 at the end
        float hop = fart > 0.6f ? Mathf.Sin((1f - fart) / 0.4f * Mathf.PI) : 0f;

        model.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(phase)) * 0.035f * go + hop * 0.10f, 0f);
        model.localRotation = Quaternion.Euler(hop * 7f, 0f, 0f);

        // diagonal pairs swing together
        float swing = Mathf.Sin(phase) * 27f * go;
        if (legFL != null) legFL.localRotation = restFL * Quaternion.Euler(swing, 0f, 0f);
        if (legBR != null) legBR.localRotation = restBR * Quaternion.Euler(swing - hop * 18f, 0f, 0f);
        if (legFR != null) legFR.localRotation = restFR * Quaternion.Euler(-swing, 0f, 0f);
        if (legBL != null) legBL.localRotation = restBL * Quaternion.Euler(-swing - hop * 18f, 0f, 0f);

        // head: nods as it walks, goes down to graze, jerks up at its own fart
        float want = animal.Grazing && fart <= 0f ? 58f : Mathf.Sin(phase * 2f) * 4f * go - (fart > 0f ? 14f : 0f);
        headPitch = Mathf.Lerp(headPitch, want, 1f - Mathf.Exp(-(fart > 0f ? 12f : 3.5f) * dt));
        if (head != null) head.localRotation = restHead * Quaternion.Euler(headPitch, Mathf.Sin(t * 0.7f) * 5f, 0f);

        tailLift = Mathf.Lerp(tailLift, fart > 0f ? 82f : 0f, 1f - Mathf.Exp(-(fart > 0f ? 16f : 5f) * dt));
        if (tail != null) tail.localRotation = restTail * Quaternion.Euler(tailLift, Mathf.Sin(t * 2.7f) * 13f * (1f - tailLift / 82f), 0f);
    }
}

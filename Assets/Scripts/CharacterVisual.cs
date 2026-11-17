using UnityEngine;

/// <summary>
/// The visible body of a competitor: the skinned character model from
/// Resources/Models/Character with per-character materials, a backpack tank
/// that fills with green gas as farts are collected, and a procedural run /
/// idle animation driven straight from how fast the competitor is moving.
///
/// Bones are posed in the model's own space (X right, Y up, Z forward)
/// relative to the rest pose, so the code does not depend on how the FBX
/// importer oriented individual bones.
/// </summary>
public class CharacterVisual : MonoBehaviour
{
    const float FeetOffset = -0.9f;     // character controllers are centred 0.9 m above the feet

    struct Bone
    {
        public Transform t;
        public Quaternion restLocal, restModel;
    }

    Competitor owner;
    Transform model;
    Bone hips, spine, chest, head, armL, foreL, armR, foreR, legL, shinL, legR, shinR;

    Transform tankGas;
    Renderer tankGasRenderer;
    Vector3 gasRestScale;
    int gasAxis = 1;
    float gasShown = -1f;

    Vector3 lastPos;
    float speed, phase, idleTime;

    public static CharacterVisual Attach(Competitor owner, Color tint, bool shadowOnly)
    {
        var v = owner.gameObject.AddComponent<CharacterVisual>();
        v.Init(owner, tint, shadowOnly);
        return v;
    }

    void Init(Competitor who, Color tint, bool shadowOnly)
    {
        owner = who;

        // per-character cloth; everything else is shared
        Color cloth = Color.Lerp(tint, Color.white, 0.08f);
        var suit = Mats.Tinted("Fabric", cloth, 5f);
        var trim = Mats.Tinted("Fabric", Color.Lerp(tint * 0.28f, new Color(0.08f, 0.08f, 0.09f), 0.5f), 5f);
        var pack = Mats.Tiled("Leather", 1.25f);

        Material MaterialFor(string slot)
        {
            switch (slot)
            {
                case "Suit": return suit;
                case "SuitTrim": return trim;
                case "Skin": return Mats.Get("Skin");
                case "Boot": return Mats.Get("Rubber");
                case "Glove": return Mats.Get("Glove");
                case "EyeWhite": return Mats.Get("EyeWhite");
                case "Pupil": return Mats.Get("Pupil");
                case "Hair": return Mats.Get("Hair");
                case "PackFabric": return pack;
                case "PackMetal": return Mats.Get("Brass");
                case "TankGlass": return Mats.Get("TankGlass");
                case "Gas": return Mats.Get("Gas");
                default: return null;      // keep whatever the model came with
            }
        }

        var wrapper = ArenaBuilder.Spawn("Character", transform.position + Vector3.up * FeetOffset,
            transform.rotation, transform, MaterialFor);
        model = wrapper.transform;

        // imported rigs come with an Animator; the bones are driven from here instead
        foreach (var a in wrapper.GetComponentsInChildren<Animator>()) Destroy(a);

        foreach (var r in wrapper.GetComponentsInChildren<Renderer>(true))
        {
            if (shadowOnly) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
        }

        hips = Find("Hips"); spine = Find("Spine"); chest = Find("Chest"); head = Find("Head");
        armL = Find("UpperArm.L"); foreL = Find("LowerArm.L");
        armR = Find("UpperArm.R"); foreR = Find("LowerArm.R");
        legL = Find("UpperLeg.L"); shinL = Find("LowerLeg.L");
        legR = Find("UpperLeg.R"); shinR = Find("LowerLeg.R");

        tankGas = FindDeep(model, "TankGas");
        if (tankGas != null)
        {
            tankGasRenderer = tankGas.GetComponent<Renderer>();
            gasRestScale = tankGas.localScale;
            // which of the tank's own axes points up? (depends on the importer)
            Vector3 up = tankGas.InverseTransformDirection(model.up);
            float ax = Mathf.Abs(up.x), ay = Mathf.Abs(up.y), az = Mathf.Abs(up.z);
            gasAxis = ax > ay && ax > az ? 0 : (ay > az ? 1 : 2);
        }

        lastPos = transform.position;
        phase = Random.value * 6f;
        idleTime = Random.value * 10f;
    }

    Bone Find(string boneName)
    {
        var t = FindDeep(model, boneName);
        if (t == null) return default;
        return new Bone
        {
            t = t,
            restLocal = t.localRotation,
            restModel = Quaternion.Inverse(model.rotation) * t.rotation,
        };
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var hit = FindDeep(root.GetChild(i), name);
            if (hit != null) return hit;
        }
        return null;
    }

    // Applies a rotation expressed in model space on top of the bone's rest pose.
    static void Pose(in Bone b, Quaternion modelRotation)
    {
        if (b.t == null) return;
        b.t.localRotation = b.restLocal * (Quaternion.Inverse(b.restModel) * modelRotation * b.restModel);
    }

    static Quaternion X(float deg) => Quaternion.AngleAxis(deg, Vector3.right);
    static Quaternion Y(float deg) => Quaternion.AngleAxis(deg, Vector3.up);
    static Quaternion Z(float deg) => Quaternion.AngleAxis(deg, Vector3.forward);

    void LateUpdate()
    {
        if (model == null) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // ground speed, smoothed
        Vector3 p = transform.position;
        Vector3 d = p - lastPos;
        d.y = 0f;
        lastPos = p;
        speed = Mathf.Lerp(speed, d.magnitude / dt, 1f - Mathf.Exp(-12f * dt));

        float run = Mathf.Clamp01(speed / 5f);          // 0 standing … 1 flat out
        phase += speed * 1.75f * dt;                    // stride frequency follows speed
        idleTime += dt;
        float s = Mathf.Sin(phase), c = Mathf.Cos(phase);
        float breathe = Mathf.Sin(idleTime * 1.8f);
        float idle = 1f - run;

        // legs: thighs swing opposite each other, knees fold as each leg comes through
        // (negative X = forward: a rotation about +X carries "down" toward "back")
        Pose(legL, X(-s * 34f * run));
        Pose(legR, X(s * 34f * run));
        Pose(shinL, X(Mathf.Max(0f, Mathf.Sin(phase + 1.25f)) * 62f * run));
        Pose(shinR, X(Mathf.Max(0f, Mathf.Sin(phase + 1.25f + Mathf.PI)) * 62f * run));

        // arms: counter-swing, tucked in and bent when running, hanging loose when idle
        float tuck = Mathf.Lerp(14f, 30f, run);
        Pose(armL, X(s * 40f * run) * Z(tuck + breathe * 1.5f * idle));
        Pose(armR, X(-s * 40f * run) * Z(-tuck - breathe * 1.5f * idle));
        Pose(foreL, X(-(10f + 48f * run) - Mathf.Max(0f, -s) * 18f * run));
        Pose(foreR, X(-(10f + 48f * run) - Mathf.Max(0f, s) * 18f * run));

        // torso: lean into the run, twist against the hips, breathe when standing
        Pose(hips, Y(-s * 6f * run));
        Pose(spine, X(7f * run + breathe * 1.2f * idle) * Y(s * 8f * run));
        Pose(chest, X(4f * run));
        Pose(head, X(-5f * run) * Y(Mathf.Sin(idleTime * 0.55f) * 14f * idle - s * 3f * run));

        // bounce twice per stride
        model.localPosition = new Vector3(0f, FeetOffset + Mathf.Abs(c) * 0.04f * run, 0f);

        UpdateTank(dt);
    }

    void UpdateTank(float dt)
    {
        if (tankGas == null || owner == null) return;
        float fill = owner.Capacity > 0 ? owner.Backpack / (float)owner.Capacity : 0f;
        gasShown = gasShown < 0f ? fill : Mathf.MoveTowards(gasShown, fill, dt * 1.5f);

        Vector3 sc = gasRestScale;
        sc[gasAxis] *= Mathf.Max(0.02f, gasShown);
        tankGas.localScale = sc;
        if (tankGasRenderer != null && !owner.Stunned)
            tankGasRenderer.enabled = gasShown > 0.01f;
    }
}

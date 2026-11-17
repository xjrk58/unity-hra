using UnityEngine;

/// <summary>
/// The fart monster's body: the model from Resources/Models/Monster (built by
/// Tools/blender/build_monster.py), wrapped in dark smoke, with burning eyes
/// that light its own face and a sick glow underneath. There is no skeleton -
/// head, jaw and arms are separate parts turning about their joints, posed here
/// every frame from what the monster is doing: it bobs and sways, watches its
/// prey with a twitching head, opens its jaws and reaches out as it closes in,
/// and lunges into the face of whoever it catches.
/// </summary>
public class MonsterVisual : MonoBehaviour
{
    FartMonster monster;
    Transform model, head, jaw, armL, armR;
    Quaternion restHead, restJaw, restArmL, restArmR;
    Light faceLight, underLight;
    ParticleSystem smoke;

    const float FaceLight = 1.5f;
    float headYaw, headPitch, jawOpen, reach, lunge;
    float twitchYaw, twitchRoll, twitchUntil, nextTwitch;

    /// <summary>A point on its face: where a caught player is made to look.</summary>
    public Transform Face { get; private set; }

    public static MonsterVisual Attach(FartMonster monster)
    {
        var v = monster.gameObject.AddComponent<MonsterVisual>();
        v.monster = monster;
        v.Build();
        return v;
    }

    void Build()
    {
        model = ArenaBuilder.Spawn("Monster", transform.position, transform.rotation, transform, slot =>
        {
            switch (slot)
            {
                case "MonsterEye": return Mats.Get("MonsterEye");
                case "MonsterMaw": return Mats.Get("MonsterMaw");
                case "MonsterTooth": return Mats.Get("MonsterTooth");
                case "MonsterClaw": return Mats.Get("MonsterClaw");
                default: return Mats.Get("MonsterBody");
            }
        }).transform;

        foreach (var t in model.GetComponentsInChildren<Transform>(true))
        {
            switch (t.name)
            {
                case "MonsterHead": head = t; break;
                case "MonsterJaw": jaw = t; break;
                case "MonsterArmL": armL = t; break;
                case "MonsterArmR": armR = t; break;
            }
        }
        if (jaw != null && head != null) jaw.SetParent(head, true);      // the jaw moves with the head
        if (head != null) restHead = head.localRotation;
        if (jaw != null) restJaw = jaw.localRotation;
        if (armL != null) restArmL = armL.localRotation;
        if (armR != null) restArmR = armR.localRotation;

        // (positions below are in the model's own space: +Z is its front, +Y up)
        Transform headOrModel = head != null ? head : model;
        Face = new GameObject("Face").transform;
        Face.SetParent(headOrModel, true);
        Face.position = model.TransformPoint(new Vector3(0f, 2.80f, 1.0f));

        // lit from under the chin, the way you hold a torch to tell a ghost story
        faceLight = AddLight("FaceLight", headOrModel, new Vector3(0f, 2.15f, 1.75f), new Color(0.80f, 1f, 0.62f), FaceLight, 3.4f);
        underLight = AddLight("UnderGlow", model, new Vector3(0f, 0.55f, 0.55f), new Color(0.40f, 1f, 0.15f), 5f, 7f);

        smoke = GasFx.MonsterSmoke(transform);
        model.gameObject.SetActive(false);
        smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    Light AddLight(string name, Transform parent, Vector3 modelSpace, Color color, float intensity, float range)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, true);
        go.transform.position = model.TransformPoint(modelSpace);
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        return l;
    }

    void LateUpdate() => Pose(Time.deltaTime);

    /// <summary>Jumps straight to the pose of the current state (dev tools: screenshots without frames in between).</summary>
    public void Settle()
    {
        twitchUntil = 0f;
        nextTwitch = float.MaxValue;
        for (int i = 0; i < 12; i++) Pose(0.25f);
        if (monster.Visible) smoke.Simulate(2.5f, true, true);
    }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    void Pose(float dt)
    {
        bool show = monster.Visible;
        if (model.gameObject.activeSelf != show)
        {
            model.gameObject.SetActive(show);
            if (show) smoke.Play(true);
            else smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        if (!show) return;

        float t = Time.time;
        var state = monster.State;
        bool feeding = state == FartMonster.Phase.Feeding;
        Competitor prey = feeding ? monster.Victim : monster.Target;

        // boiling up out of the floor / sinking back into it
        float k = 1f;
        if (state == FartMonster.Phase.Emerging) k = EaseOutBack(Mathf.Clamp01(monster.StateTime / FartMonster.EmergeSeconds));
        else if (state == FartMonster.Phase.Dissolving)
        {
            float u = Mathf.Clamp01(monster.StateTime / FartMonster.DissolveSeconds);
            k = 1f - u * u;
        }
        float wide = Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(k));
        model.localScale = new Vector3(wide, Mathf.Max(0.02f, k), wide);

        lunge = Mathf.MoveTowards(lunge, feeding ? 1f : 0f, dt * 7f);
        float hover = 0.16f + 0.07f * Mathf.Sin(t * 1.3f);
        model.localPosition = new Vector3(0f, hover - 0.62f * lunge, 0f);
        float lean = 4f + 7f * Mathf.Clamp01(monster.Speed / 6.6f) + 21f * lunge;
        model.localRotation = Quaternion.Euler(lean, 0f, Mathf.Sin(t * 0.9f) * 2.5f + (feeding ? Mathf.Sin(t * 37f) * 1.5f : 0f));

        // how close the prey is: 1 within 2 m, 0 beyond 7 m
        float close = 0f;
        Vector3 lookAt = transform.position + transform.forward * 6f + Vector3.up * 1.6f;
        if (prey != null)
        {
            Vector3 d = prey.transform.position - transform.position;
            d.y = 0f;
            close = Mathf.Clamp01(1f - (d.magnitude - 2f) / 5f);
            lookAt = prey.transform.position + Vector3.up * 0.6f;
        }

        // jaws: slack while it prowls, gaping as it closes in, torn wide open for the roar
        float wantJaw = feeding || state == FartMonster.Phase.Emerging
            ? 52f + Mathf.Sin(t * 41f) * 3f
            : Mathf.Lerp(12f, 36f, close) + Mathf.Sin(t * 2.3f) * 5f;
        jawOpen = Mathf.Lerp(jawOpen, wantJaw, 1f - Mathf.Exp(-10f * dt));
        if (jaw != null) jaw.localRotation = restJaw * Quaternion.Euler(jawOpen, 0f, 0f);

        // head: fixed on the prey, with sudden small twitches
        if (head != null)
        {
            Vector3 local = model.InverseTransformPoint(lookAt) - model.InverseTransformPoint(head.position);
            float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
            float f = 1f - Mathf.Exp(-8f * dt);
            headYaw = Mathf.Lerp(headYaw, Mathf.Clamp(yaw, -55f, 55f), f);
            headPitch = Mathf.Lerp(headPitch, Mathf.Clamp(pitch, -30f, 42f), f);
            if (t >= nextTwitch)
            {
                twitchYaw = Random.Range(-15f, 15f);
                twitchRoll = Random.Range(-13f, 13f);
                twitchUntil = t + Random.Range(0.08f, 0.2f);
                nextTwitch = t + Random.Range(0.5f, 2.4f);
            }
            bool twitching = t < twitchUntil;
            head.localRotation = restHead * Quaternion.Euler(headPitch, headYaw + (twitching ? twitchYaw : 0f), twitching ? twitchRoll : 0f);
        }

        // arms: hanging and swaying, then reaching for the prey
        reach = Mathf.Lerp(reach, feeding ? 1f : close, 1f - Mathf.Exp(-6f * dt));
        float swing = Mathf.Sin(t * 1.15f) * 9f;
        float shake = feeding ? Mathf.Sin(t * 33f) * 3f : 0f;
        if (armL != null) armL.localRotation = restArmL * Quaternion.Euler(-6f + swing - 76f * reach + shake, 0f, -6f + 12f * reach);
        if (armR != null) armR.localRotation = restArmR * Quaternion.Euler(-6f - swing - 76f * reach - shake, 0f, 6f - 12f * reach);

        // its own light gutters like a bad lamp
        float gutter = 0.8f + 0.2f * Mathf.PerlinNoise(t * 7f, 1.3f);
        faceLight.intensity = FaceLight * gutter * Mathf.Clamp01(k);
        underLight.intensity = 5f * gutter * Mathf.Clamp01(k);
    }
}

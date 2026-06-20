using UnityEngine;

/// <summary>
/// Single entry point placed in the scene. Builds the entire game world from
/// primitives at runtime: lighting, arena, goal, player, AI rivals and pickups.
/// Keeping everything code-driven means the authored scene only needs this one
/// component.
/// </summary>
public class GameBootstrap : MonoBehaviour
{
    const float ArenaHalf = 20f;
    static readonly Vector3 GoalPos = new Vector3(0f, 0f, 16f);

    void Awake()
    {
        BuildManager();
        BuildEnvironment();
        BuildGoal();
        BuildPlayer(new Vector3(0f, 1.2f, -14f));
        BuildAI("Pšouk", new Color(0.85f, 0.2f, 0.2f), new Vector3(-7f, 1.2f, -7f));
        BuildAI("Bzdina", new Color(0.2f, 0.45f, 0.9f), new Vector3(7f, 1.2f, -7f));
        SpawnPickups(14);
    }

    void BuildManager()
    {
        var go = new GameObject("GameManager");
        go.AddComponent<GameManager>();
    }

    void BuildEnvironment()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.45f, 0.47f, 0.52f);

        var sunGO = new GameObject("Sun");
        var sun = sunGO.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.96f, 0.86f);
        sun.intensity = 1.1f;
        sun.shadows = LightShadows.Soft;
        sunGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.localScale = new Vector3(ArenaHalf * 0.2f, 1f, ArenaHalf * 0.2f);
        floor.GetComponent<Renderer>().material = MaterialUtil.Opaque(new Color(0.3f, 0.32f, 0.34f));

        CreateWall(new Vector3(0f, 2f, ArenaHalf), new Vector3(ArenaHalf * 2f, 4f, 1f));
        CreateWall(new Vector3(0f, 2f, -ArenaHalf), new Vector3(ArenaHalf * 2f, 4f, 1f));
        CreateWall(new Vector3(ArenaHalf, 2f, 0f), new Vector3(1f, 4f, ArenaHalf * 2f));
        CreateWall(new Vector3(-ArenaHalf, 2f, 0f), new Vector3(1f, 4f, ArenaHalf * 2f));

        var obsMat = MaterialUtil.Opaque(new Color(0.42f, 0.36f, 0.3f));
        Vector3[] obstacles =
        {
            new Vector3(8f, 1f, 4f), new Vector3(-7f, 1f, 9f), new Vector3(5f, 1f, -9f),
            new Vector3(-10f, 1f, -3f), new Vector3(0f, 1f, 8f),
        };
        foreach (var p in obstacles)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.name = "Obstacle";
            b.transform.position = p;
            b.transform.localScale = new Vector3(3f, 2f, 3f);
            b.GetComponent<Renderer>().material = obsMat;
        }
    }

    void CreateWall(Vector3 pos, Vector3 size)
    {
        var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
        w.name = "Wall";
        w.transform.position = pos;
        w.transform.localScale = size;
        w.GetComponent<Renderer>().material = MaterialUtil.Opaque(new Color(0.22f, 0.23f, 0.26f));
    }

    void BuildGoal()
    {
        var goal = new GameObject("Cil");
        goal.transform.position = GoalPos;

        var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pad.name = "GoalPad";
        pad.transform.SetParent(goal.transform, false);
        pad.transform.localScale = new Vector3(6f, 0.2f, 6f);
        pad.transform.localPosition = new Vector3(0f, 0.1f, 0f);
        pad.GetComponent<Renderer>().material = MaterialUtil.Emissive(new Color(1f, 0.55f, 0.05f), 1.2f);
        Destroy(pad.GetComponent<Collider>());

        var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beam.name = "GoalBeam";
        beam.transform.SetParent(goal.transform, false);
        beam.transform.localScale = new Vector3(0.3f, 4f, 0.3f);
        beam.transform.localPosition = new Vector3(0f, 4f, 0f);
        beam.GetComponent<Renderer>().material = MaterialUtil.Emissive(new Color(1f, 0.8f, 0.2f), 1.5f);
        Destroy(beam.GetComponent<Collider>());

        var trigger = goal.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(6f, 4f, 6f);
        trigger.center = new Vector3(0f, 2f, 0f);
        goal.AddComponent<GoalZone>();

        GameManager.Instance.GoalTransform = goal.transform;
    }

    void BuildPlayer(Vector3 pos)
    {
        var player = new GameObject("Player");
        player.transform.position = pos;

        var cc = player.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.4f;
        cc.center = Vector3.zero;

        var camGO = new GameObject("PlayerCamera");
        camGO.transform.SetParent(player.transform, false);
        camGO.transform.localPosition = new Vector3(0f, 0.7f, 0f);
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.5f, 0.7f, 0.95f);
        camGO.AddComponent<AudioListener>();

        var pc = player.AddComponent<PlayerController>();
        pc.Tint = new Color(0.3f, 0.9f, 0.4f);
        GameManager.Instance.RegisterCompetitor(pc);
    }

    void BuildAI(string name, Color tint, Vector3 pos)
    {
        var go = new GameObject("AI_" + name);
        go.transform.position = pos;

        var cc = go.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.4f;
        cc.center = Vector3.zero;

        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(go.transform, false);
        body.transform.localScale = new Vector3(0.8f, 0.9f, 0.8f);
        Destroy(body.GetComponent<Collider>());
        body.GetComponent<Renderer>().material = MaterialUtil.Opaque(tint);

        var nose = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        nose.name = "Nose";
        nose.transform.SetParent(go.transform, false);
        nose.transform.localScale = Vector3.one * 0.3f;
        nose.transform.localPosition = new Vector3(0f, 0.4f, 0.45f);
        Destroy(nose.GetComponent<Collider>());
        nose.GetComponent<Renderer>().material = MaterialUtil.Opaque(Color.white);

        var pack = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pack.name = "Backpack";
        pack.transform.SetParent(go.transform, false);
        pack.transform.localScale = new Vector3(0.5f, 0.6f, 0.3f);
        pack.transform.localPosition = new Vector3(0f, 0.3f, -0.45f);
        Destroy(pack.GetComponent<Collider>());
        pack.GetComponent<Renderer>().material = MaterialUtil.Opaque(new Color(0.35f, 0.25f, 0.15f));

        var ai = go.AddComponent<AICompetitor>();
        ai.DisplayName = name;
        ai.Tint = tint;
        GameManager.Instance.RegisterCompetitor(ai);
    }

    void SpawnPickups(int count)
    {
        var container = new GameObject("Pickups");
        var mat = MaterialUtil.Emissive(new Color(0.5f, 0.85f, 0.2f), 0.7f);

        int placed = 0, guard = 0;
        while (placed < count && guard++ < 1000)
        {
            var p = new Vector3(Random.Range(-17f, 17f), 0.6f, Random.Range(-17f, 17f));
            if (Vector3.Distance(p, GoalPos + Vector3.up * 0.6f) < 5f) continue;

            var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            s.name = "FartPickup";
            s.transform.SetParent(container.transform, true);
            s.transform.position = p;
            s.transform.localScale = Vector3.one * 0.7f;
            s.GetComponent<Renderer>().material = mat;

            var col = s.GetComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 1.7f; // generous pickup range relative to the 0.7 scale

            s.AddComponent<FartPickup>();
            placed++;
        }
    }
}

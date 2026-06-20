using UnityEngine;

/// <summary>
/// Single entry point placed in the scene. Builds the entire game world from
/// primitives at runtime: lighting, a multi-storey arena, ramps, the goal, the
/// player, AI rivals and pickups. Geometry comes from <see cref="Arena"/> so the
/// AI navigation matches the built world.
/// </summary>
public class GameBootstrap : MonoBehaviour
{
    static readonly Vector3 GoalPos = new Vector3(9f, 0f, -9f);

    Material floorMat, rampMat, railMat;

    void Awake()
    {
        floorMat = MaterialUtil.Opaque(new Color(0.32f, 0.34f, 0.37f));
        rampMat = MaterialUtil.Opaque(new Color(0.5f, 0.42f, 0.28f));
        railMat = MaterialUtil.Opaque(new Color(0.22f, 0.23f, 0.26f));

        BuildManager();
        BuildEnvironment();
        BuildGoal();
        BuildPlayer(new Vector3(0f, 1.2f, -6f));
        BuildAI("Pšouk", new Color(0.85f, 0.2f, 0.2f), new Vector3(-6f, 1.2f, -2f));
        BuildAI("Bzdina", new Color(0.2f, 0.45f, 0.9f), new Vector3(6f, 1.2f, -2f));
        SpawnPickups(20);
    }

    void BuildManager()
    {
        new GameObject("GameManager").AddComponent<GameManager>();
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

        for (int f = 0; f < Arena.FloorCount; f++)
            BuildFloor(f);

        for (int i = 0; i < Arena.FloorCount - 1; i++)
            BuildRamp(i);
    }

    // A floor is a solid square platform; upper floors have a hole the ramp
    // rises through. The platform is assembled from strips around that hole.
    void BuildFloor(int f)
    {
        float y = Arena.FloorY(f);
        float h = Arena.Half;

        if (f == 0)
        {
            AddBox("Floor", 0f, y - 0.25f, 0f, h * 2f, 0.5f, h * 2f, floorMat);
        }
        else
        {
            float s = Arena.Side(f - 1);
            float zLo = Mathf.Min(Arena.HoleZMin * s, Arena.HoleZMax * s);
            float zHi = Mathf.Max(Arena.HoleZMin * s, Arena.HoleZMax * s);
            float xLo = -Arena.HoleXHalf, xHi = Arena.HoleXHalf;

            // back strip (z below hole) and front strip (z above hole)
            AddStrip("Floor", -h, h, -h, zLo, y);
            AddStrip("Floor", -h, h, zHi, h, y);
            // left / right strips beside the hole
            AddStrip("Floor", -h, xLo, zLo, zHi, y);
            AddStrip("Floor", xHi, h, zLo, zHi, y);
        }

        // perimeter railings so players/AI don't walk off the edges
        AddBox("Rail", 0f, y + 0.6f, h, h * 2f, 1.2f, 0.4f, railMat);
        AddBox("Rail", 0f, y + 0.6f, -h, h * 2f, 1.2f, 0.4f, railMat);
        AddBox("Rail", h, y + 0.6f, 0f, 0.4f, 1.2f, h * 2f, railMat);
        AddBox("Rail", -h, y + 0.6f, 0f, 0.4f, 1.2f, h * 2f, railMat);
    }

    // Adds a floor slab covering [xMin,xMax] x [zMin,zMax] at height y (skips empty).
    void AddStrip(string name, float xMin, float xMax, float zMin, float zMax, float y)
    {
        float sx = xMax - xMin, sz = zMax - zMin;
        if (sx <= 0.01f || sz <= 0.01f) return;
        AddBox(name, (xMin + xMax) * 0.5f, y - 0.25f, (zMin + zMax) * 0.5f, sx, 0.5f, sz, floorMat);
    }

    void BuildRamp(int i)
    {
        float s = Arena.Side(i);
        float run = Arena.RampFootZ - Arena.RampTopZ; // horizontal length in Z
        float rise = Arena.FloorHeight;                // vertical climb
        float length = Mathf.Sqrt(run * run + rise * rise);
        float angle = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
        float centerZ = (Arena.RampFootZ + Arena.RampTopZ) * 0.5f * s;

        var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ramp.name = "Ramp";
        ramp.transform.localScale = new Vector3(Arena.RampWidth, 0.5f, length);
        ramp.transform.position = new Vector3(0f, Arena.FloorY(i) + rise * 0.5f, centerZ);
        // pitch so the foot (near the edge) sits low and the top reaches the floor above
        ramp.transform.rotation = Quaternion.Euler(angle * s, 0f, 0f);
        ramp.GetComponent<Renderer>().material = rampMat;
    }

    GameObject AddBox(string name, float cx, float cy, float cz, float sx, float sy, float sz, Material mat)
    {
        var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
        b.name = name;
        b.transform.position = new Vector3(cx, cy, cz);
        b.transform.localScale = new Vector3(sx, sy, sz);
        b.GetComponent<Renderer>().material = mat;
        return b;
    }

    void BuildGoal()
    {
        var goal = new GameObject("Cil");
        goal.transform.position = GoalPos;

        var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pad.name = "GoalPad";
        pad.transform.SetParent(goal.transform, false);
        pad.transform.localScale = new Vector3(5f, 0.2f, 5f);
        pad.transform.localPosition = new Vector3(0f, 0.1f, 0f);
        pad.GetComponent<Renderer>().material = MaterialUtil.Emissive(new Color(1f, 0.55f, 0.05f), 1.2f);
        Destroy(pad.GetComponent<Collider>());

        var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beam.name = "GoalBeam";
        beam.transform.SetParent(goal.transform, false);
        beam.transform.localScale = new Vector3(0.3f, 10f, 0.3f);
        beam.transform.localPosition = new Vector3(0f, 10f, 0f);
        beam.GetComponent<Renderer>().material = MaterialUtil.Emissive(new Color(1f, 0.8f, 0.2f), 1.5f);
        Destroy(beam.GetComponent<Collider>());

        var trigger = goal.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(5f, 4f, 5f);
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
        cam.farClipPlane = 200f;
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

    // Scatter pickups across every floor, avoiding holes and the goal.
    void SpawnPickups(int count)
    {
        var container = new GameObject("Pickups");
        var mat = MaterialUtil.Emissive(new Color(0.5f, 0.85f, 0.2f), 0.7f);

        int placed = 0, guard = 0;
        while (placed < count && guard++ < 2000)
        {
            int f = Random.Range(0, Arena.FloorCount);
            float x = Random.Range(-11f, 11f);
            float z = Random.Range(-11f, 11f);
            if (InHole(f, x, z)) continue;
            if (f == 0 && Vector2.Distance(new Vector2(x, z), new Vector2(GoalPos.x, GoalPos.z)) < 4f)
                continue;

            var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            s.name = "FartPickup";
            s.transform.SetParent(container.transform, true);
            s.transform.position = new Vector3(x, Arena.FloorY(f) + 0.6f, z);
            s.transform.localScale = Vector3.one * 0.7f;
            s.GetComponent<Renderer>().material = mat;

            var col = s.GetComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 1.7f;

            s.AddComponent<FartPickup>();
            placed++;
        }
    }

    static bool InHole(int f, float x, float z)
    {
        if (f == 0) return false;
        float s = Arena.Side(f - 1);
        float zLo = Mathf.Min(Arena.HoleZMin * s, Arena.HoleZMax * s);
        float zHi = Mathf.Max(Arena.HoleZMin * s, Arena.HoleZMax * s);
        return Mathf.Abs(x) <= Arena.HoleXHalf + 1f && z >= zLo - 1f && z <= zHi + 1f;
    }
}

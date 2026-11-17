using UnityEngine;

/// <summary>
/// Single entry point placed in the scene. Builds the entire game world at
/// runtime: lighting and sky (<see cref="LightingRig"/>), the multi-storey
/// arena (<see cref="ArenaBuilder"/>), the goal, the player, AI rivals and
/// pickups. Geometry comes from <see cref="Arena"/> so the AI navigation
/// matches the built world.
/// </summary>
public class GameBootstrap : MonoBehaviour
{
    public int pickupCount = 20;

    /// <summary>Lowest deck pickups may spawn on (0 = everywhere). Raised by the automated playtest to force ramp use.</summary>
    public static int PickupMinFloor;

    void Awake()
    {
        if (Application.isMobilePlatform)
        {
            Application.targetFrameRate = 60;               // phones default to 30
            Screen.sleepTimeout = SleepTimeout.NeverSleep;  // looking around is not "activity" to the OS
        }
        Session.Resolve();
        if (Session.Current == Session.Mode.Menu)
        {
            BuildMenu();
            return;
        }

        // (how long each stage takes goes to the log: a slow start on a phone shows up there)
        float bootStart = Time.realtimeSinceStartup, t0 = bootStart;
        Debug.Log("[Boot] building the world");
        new GameObject("GameManager").AddComponent<GameManager>();
        GameInput.Init();
        FartAudio.Init();
        float audioMs = (Time.realtimeSinceStartup - t0) * 1000f; t0 = Time.realtimeSinceStartup;
        LightingRig.Build();
        float lightMs = (Time.realtimeSinceStartup - t0) * 1000f; t0 = Time.realtimeSinceStartup;
        ArenaBuilder.Build();
        BuildGoal();
        float arenaMs = (Time.realtimeSinceStartup - t0) * 1000f;

        // Who is in the match, in the same order on every device: the host's player, the
        // joining player (two-player games only), then the bots.
        Color green = new Color(0.18f, 0.62f, 0.22f), yellow = new Color(0.92f, 0.72f, 0.08f);
        Color red = new Color(0.80f, 0.13f, 0.11f), blue = new Color(0.12f, 0.33f, 0.85f);
        Vector3 left = new Vector3(-1.6f, 0.95f, -6f), right = new Vector3(1.6f, 0.95f, -6f);
        Vector3 bot1 = new Vector3(-6f, 0.95f, -2f), bot2 = new Vector3(6f, 0.95f, -2f);
        FartPickup.DisplayOnly = Session.IsClient;
        switch (Session.Current)
        {
            case Session.Mode.Host:
                // this device runs the world; the other player is steered from the other device
                BuildPlayer(left, green);
                var joiner = BuildPuppet("Hráč 2", yellow, right, human: true);
                BuildAI("Pšouk", red, bot1);
                BuildAI("Bzdina", blue, bot2);
                SpawnPickups(pickupCount);
                SpawnHerd(puppets: false);
                FartMonster.Build();
                NetGame.BuildHost(joiner);
                break;

            case Session.Mode.Client:
                // this device only moves its own player; everything else is shown as the host reports it
                BuildPuppet("Hráč 1", green, left, human: true);
                BuildPlayer(right, yellow);
                BuildPuppet("Pšouk", red, bot1, human: false);
                BuildPuppet("Bzdina", blue, bot2, human: false);
                PlacePickups(Session.Pickups);
                SpawnHerd(puppets: true);
                FartMonster.Build(puppet: true);
                NetGame.BuildClient();
                break;

            default:
                BuildPlayer(new Vector3(0f, 0.95f, -6f), green);
                BuildAI("Pšouk", red, bot1);
                BuildAI("Bzdina", blue, bot2);
                SpawnPickups(pickupCount);
                SpawnHerd(puppets: false);
                FartMonster.Build();
                break;
        }
        if (Session.HasFlag("-netBot")) NetBot.Attach();
        DevCapture.StartIfRequested();
        Debug.Log($"[Boot] world built in {(Time.realtimeSinceStartup - bootStart) * 1000f:0} ms " +
                  $"(audio {audioMs:0}, lighting {lightMs:0}, arena {arenaMs:0})");
    }

    // The title screen: the empty arena under a circling camera, and the menu over it.
    void BuildMenu()
    {
        LightingRig.Build();
        ArenaBuilder.Build();
        BuildGoal();
        var camGO = new GameObject("MenuCamera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<Camera>();
        cam.fieldOfView = 55f;
        LightingRig.ConfigureCamera(cam);
        camGO.AddComponent<AudioListener>();
        camGO.AddComponent<MenuOrbit>();
        MainMenu.Build();
        if (Session.HasFlag("-captureMenu")) DevCapture.StartIfRequested();
    }

    void BuildGoal()
    {
        var goal = new GameObject("Cil");
        goal.transform.position = Arena.GoalPos;

        var paint = Mats.Get("GoalPaint");
        var steel = Mats.Get("Steel");
        var black = Mats.Get("HazardBlack");
        var glow = Mats.Get("GoalGlow");
        ArenaBuilder.Spawn("Goal", Arena.GoalPos, Quaternion.identity, goal.transform, slot =>
        {
            switch (slot)
            {
                case "Metal": return steel;
                case "HazardB": return black;
                case "Glow": return glow;
                default: return paint;      // GoalPaint, HazardA
            }
        });

        // the extractor fan up in the duct turns
        foreach (var t in goal.GetComponentsInChildren<Transform>())
            if (t.name == "GoalFan") t.gameObject.AddComponent<Spinner>();

        // warm light spilling from under the hood, so the goal glows from across the deck
        var lightGO = new GameObject("GoalLight");
        lightGO.transform.SetParent(goal.transform, false);
        lightGO.transform.localPosition = new Vector3(0f, 2.5f, 0f);
        var l = lightGO.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1f, 0.5f, 0.12f);
        l.intensity = 14f;
        l.range = 8f;
        l.shadows = LightShadows.None;

        var trigger = goal.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(5f, 4f, 5f);
        trigger.center = new Vector3(0f, 2f, 0f);
        goal.AddComponent<GoalZone>();

        if (GameManager.Instance != null) GameManager.Instance.GoalTransform = goal.transform;
    }

    static CharacterController AddController(GameObject go)
    {
        var cc = go.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.4f;
        cc.center = Vector3.zero;
        cc.stepOffset = 0.35f;
        cc.slopeLimit = 50f;
        return cc;
    }

    void BuildPlayer(Vector3 pos, Color tint)
    {
        var player = new GameObject("Player");
        player.transform.position = pos;
        AddController(player);

        var camGO = new GameObject("PlayerCamera");
        camGO.tag = "MainCamera";
        camGO.transform.SetParent(player.transform, false);
        camGO.transform.localPosition = new Vector3(0f, 0.7f, 0f);
        var cam = camGO.AddComponent<Camera>();
        cam.fieldOfView = 68f;
        LightingRig.ConfigureCamera(cam);
        camGO.AddComponent<AudioListener>();

        var pc = player.AddComponent<PlayerController>();
        pc.Tint = tint;
        GameManager.Instance.RegisterCompetitor(pc);

        // the player's own body is never drawn, but it still casts a shadow
        CharacterVisual.Attach(pc, pc.Tint, shadowOnly: true);
    }

    void BuildAI(string name, Color tint, Vector3 pos)
    {
        var go = new GameObject("AI_" + name);
        go.transform.position = pos;
        AddController(go);

        var ai = go.AddComponent<AICompetitor>();
        ai.DisplayName = name;
        ai.Tint = tint;
        GameManager.Instance.RegisterCompetitor(ai);
        CharacterVisual.Attach(ai, tint, shadowOnly: false);
    }

    // Somebody steered from the other device: the other person, or (on a joining device) a bot.
    NetPuppet BuildPuppet(string name, Color tint, Vector3 pos, bool human)
    {
        var go = new GameObject((human ? "Human_" : "AI_") + name);
        go.transform.position = pos;
        AddController(go);

        var puppet = go.AddComponent<NetPuppet>();
        puppet.human = human;
        puppet.DisplayName = name;
        puppet.Tint = tint;
        GameManager.Instance.RegisterCompetitor(puppet);
        CharacterVisual.Attach(puppet, tint, shadowOnly: false);
        return puppet;
    }

    // Joining device: the farts go where the host scattered them, in the host's order.
    void PlacePickups(System.Collections.Generic.List<Vector3> at)
    {
        var container = new GameObject("Pickups");
        foreach (var p in at) MakePickup(p, container.transform);
    }

    public static FartPickup MakePickup(Vector3 position, Transform container)
    {
        var s = new GameObject("FartPickup");
        s.transform.SetParent(container, true);
        s.transform.position = position;
        var col = s.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 1.1f;
        return s.AddComponent<FartPickup>();
    }

    // Horses and unicorns: the same animals in the same order on every device, after the
    // scattered farts (each animal brings one fart of its own).
    void SpawnHerd(bool puppets)
    {
        FartAnimal.All.Clear();
        if (!FartAnimal.Enabled) return;
        var container = GameObject.Find("Pickups");
        var pickups = container != null ? container.transform : new GameObject("Pickups").transform;
        FartAnimal.Spawn(false, 0, new Color(0.36f, 0.20f, 0.09f), pickups, puppets);     // bay horse, ground deck
        FartAnimal.Spawn(true, 1, new Color(0.93f, 0.93f, 0.97f), pickups, puppets);      // unicorn
        FartAnimal.Spawn(false, 2, new Color(0.10f, 0.08f, 0.07f), pickups, puppets);     // black horse
        FartAnimal.Spawn(true, 3, new Color(0.97f, 0.90f, 0.95f), pickups, puppets);      // unicorn on the roof
        FartAnimal.Spawn(false, 3, new Color(0.72f, 0.55f, 0.28f), pickups, puppets);     // palomino on the roof
    }

    // Scatter pickups across every deck, avoiding holes, ramps, columns and the goal.
    void SpawnPickups(int count)
    {
        var container = new GameObject("Pickups");

        int placed = 0, guard = 0;
        while (placed < count && guard++ < 4000)
        {
            int f = Random.Range(Mathf.Clamp(PickupMinFloor, 0, Arena.FloorCount - 1), Arena.FloorCount);
            float x = Random.Range(-10.6f, 10.6f);
            float z = Random.Range(-10.6f, 10.6f);
            if (Arena.InHole(f, x, z, 1.3f)) continue;
            if (Arena.OnRampFootprint(f, x, z, 1.0f)) continue;
            if (NearColumn(x, z)) continue;
            if (f == 0 && Vector2.Distance(new Vector2(x, z), new Vector2(Arena.GoalPos.x, Arena.GoalPos.z)) < 4.2f)
                continue;

            MakePickup(new Vector3(x, Arena.FloorY(f) + 0.75f, z), container.transform);
            placed++;
        }
    }

    static bool NearColumn(float x, float z)
    {
        foreach (float gx in Arena.Grid)
            foreach (float gz in Arena.Grid)
                if (Mathf.Abs(x - gx) < 1.3f && Mathf.Abs(z - gz) < 1.3f) return true;
        return false;
    }
}

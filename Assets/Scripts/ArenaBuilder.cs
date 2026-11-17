using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds the visible arena: a four-deck open concrete structure. Large flat
/// parts (slabs, ramps, kerbs, beams) are generated here with real-world UVs;
/// detailed parts (railings, columns, lamps, props) are FBX models from
/// Resources/Models placed procedurally. All dimensions come from
/// <see cref="Arena"/> so the look, the collision and the bots' navigation agree.
/// </summary>
public static class ArenaBuilder
{
    const float FloorTile = 3f;      // metres per texture repeat
    const float WallTile = 2f;
    const float KerbH = 0.18f, KerbW = 0.3f;
    const float RailLine = Arena.Half - KerbW * 0.5f;   // centre line of the perimeter kerb + railing
    const float ModuleLen = 2f;                         // one railing section

    static Transform root;
    static Material floorMat, wallMat, asphaltMat;
    static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();

    public static void Build()
    {
        root = new GameObject("Arena").transform;
        prefabs.Clear();
        floorMat = Mats.Get("ConcreteFloor");
        wallMat = Mats.Get("ConcreteWall");
        asphaltMat = Mats.Get("Asphalt");

        NavPlanner.Clear();
        BuildGround();
        for (int f = 0; f < Arena.FloorCount; f++) BuildFloor(f);
        for (int i = 0; i < Arena.FloorCount - 1; i++) BuildRamp(i);
        BuildStructure();
        BuildMarkings();
        BuildLamps();
        BuildProps();
    }

    // ------------------------------------------------------------------ helpers

    static GameObject Box(string name, Vector3 center, Vector3 size, Material top, Material side,
        float topTile, float sideTile, bool collider = true, Transform parent = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent != null ? parent : root, false);
        go.transform.localPosition = center;
        go.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Box(size, center, topTile, sideTile);
        go.AddComponent<MeshRenderer>().sharedMaterials = new[] { top, side };
        if (collider) go.AddComponent<BoxCollider>().size = size;
        return go;
    }

    static void Concrete(string name, Vector3 center, Vector3 size, bool collider = false) =>
        Box(name, center, size, wallMat, wallMat, WallTile, WallTile, collider);

    static void Blocker(string name, Vector3 center, Vector3 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.localPosition = center;
        go.AddComponent<BoxCollider>().size = size;
    }

    /// <summary>
    /// Instantiates an FBX from Resources/Models under a clean wrapper object
    /// (so the importer's own axis/scale fix-ups never leak into game code) and
    /// swaps every material slot for a library material chosen by slot name.
    /// </summary>
    public static GameObject Spawn(string model, Vector3 position, Quaternion rotation, Transform parent,
        Func<string, Material> materialFor)
    {
        var wrapper = new GameObject(model.Substring(model.LastIndexOf('/') + 1));
        wrapper.transform.SetParent(parent, false);
        wrapper.transform.SetPositionAndRotation(position, rotation);

        if (!prefabs.TryGetValue(model, out var prefab))
        {
            prefab = Resources.Load<GameObject>("Models/" + model);
            if (prefab == null) Debug.LogWarning("Model missing: Resources/Models/" + model);
            prefabs[model] = prefab;
        }
        if (prefab == null) return wrapper;

        var inst = UnityEngine.Object.Instantiate(prefab, wrapper.transform, false);
        inst.name = "Model";
        foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = materialFor(SlotName(mats[i]));
                if (m != null) mats[i] = m;
            }
            r.sharedMaterials = mats;
        }
        return wrapper;
    }

    static string SlotName(Material m)
    {
        if (m == null) return "";
        string n = m.name;
        int cut = n.IndexOf(" (", StringComparison.Ordinal);
        return cut > 0 ? n.Substring(0, cut) : n;
    }

    // ------------------------------------------------------------------ ground

    static void BuildGround()
    {
        var ground = new GameObject("Ground");
        ground.transform.SetParent(root, false);
        ground.transform.localPosition = new Vector3(0f, -0.08f, 0f);
        ground.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Ground(1400f, 46f);
        ground.AddComponent<MeshRenderer>().sharedMaterial = Mats.Get("Ground");
        var col = ground.AddComponent<BoxCollider>();
        col.size = new Vector3(1400f, 1f, 1400f);
        col.center = new Vector3(0f, -0.5f, 0f);

        // asphalt apron the building stands on
        Box("Apron", new Vector3(0f, -0.28f, 0f), new Vector3(44f, 0.5f, 44f), asphaltMat, wallMat, 5.5f, WallTile, false);
    }

    // ------------------------------------------------------------------ decks

    // A deck is a solid square slab; upper decks have a hole the ramp rises
    // through, so the slab is assembled from strips around that hole.
    static void BuildFloor(int f)
    {
        float y = Arena.FloorY(f);
        float h = Arena.Half;

        if (f == 0)
        {
            Slab(-h, h, -h, h, y);
        }
        else
        {
            Arena.HoleZ(f, out float zLo, out float zHi);
            float xLo = -Arena.HoleXHalf, xHi = Arena.HoleXHalf;
            Slab(-h, h, -h, zLo, y);        // back strip (z below hole)
            Slab(-h, h, zHi, h, y);         // front strip (z above hole)
            Slab(-h, xLo, zLo, zHi, y);     // left / right strips beside the hole
            Slab(xHi, h, zLo, zHi, y);
            BuildHoleGuard(f);
        }

        // perimeter: invisible wall for the physics, kerb + steel railing for the eyes.
        // The wall is a full storey high: nobody hops over it from a crate or a barrel.
        float wallH = Arena.FloorHeight - Arena.SlabThickness;
        Blocker("EdgeWall", new Vector3(0f, y + wallH * 0.5f, h), new Vector3(h * 2f, wallH, 0.4f));
        Blocker("EdgeWall", new Vector3(0f, y + wallH * 0.5f, -h), new Vector3(h * 2f, wallH, 0.4f));
        Blocker("EdgeWall", new Vector3(h, y + wallH * 0.5f, 0f), new Vector3(0.4f, wallH, h * 2f));
        Blocker("EdgeWall", new Vector3(-h, y + wallH * 0.5f, 0f), new Vector3(0.4f, wallH, h * 2f));

        float ky = y + KerbH * 0.5f;
        Concrete("Kerb", new Vector3(0f, ky, RailLine), new Vector3(h * 2f, KerbH, KerbW));
        Concrete("Kerb", new Vector3(0f, ky, -RailLine), new Vector3(h * 2f, KerbH, KerbW));
        Concrete("Kerb", new Vector3(RailLine, ky, 0f), new Vector3(KerbW, KerbH, h * 2f - KerbW * 2f));
        Concrete("Kerb", new Vector3(-RailLine, ky, 0f), new Vector3(KerbW, KerbH, h * 2f - KerbW * 2f));

        float span = h * 2f - KerbW;
        RailRun(new Vector3(-span * 0.5f, y + KerbH, RailLine), new Vector3(span * 0.5f, y + KerbH, RailLine));
        RailRun(new Vector3(-span * 0.5f, y + KerbH, -RailLine), new Vector3(span * 0.5f, y + KerbH, -RailLine));
        RailRun(new Vector3(RailLine, y + KerbH, -span * 0.5f), new Vector3(RailLine, y + KerbH, span * 0.5f));
        RailRun(new Vector3(-RailLine, y + KerbH, -span * 0.5f), new Vector3(-RailLine, y + KerbH, span * 0.5f));
    }

    static void Slab(float xMin, float xMax, float zMin, float zMax, float y)
    {
        float sx = xMax - xMin, sz = zMax - zMin;
        if (sx <= 0.01f || sz <= 0.01f) return;
        Box("Slab", new Vector3((xMin + xMax) * 0.5f, y - Arena.SlabThickness * 0.5f, (zMin + zMax) * 0.5f),
            new Vector3(sx, Arena.SlabThickness, sz), floorMat, wallMat, FloorTile, WallTile);
    }

    // Railings (and matching colliders) along the stairwell hole of deck f, so
    // nobody drops through it; the end facing the deck centre stays open where
    // the ramp arrives.
    static void BuildHoleGuard(int f)
    {
        float y = Arena.FloorY(f);
        float s = Arena.Side(f - 1);
        float x = Arena.HoleXHalf + KerbW * 0.5f;
        float zNear = (Arena.HoleZMin - KerbW * 0.5f) * s;      // open end, toward the deck centre
        float zFar = (Arena.Half - KerbW) * s;                   // runs into the perimeter kerb
        float zMid = (zNear + zFar) * 0.5f, zLen = Mathf.Abs(zFar - zNear);

        for (int side = -1; side <= 1; side += 2)
        {
            float sx = x * side;
            Blocker("HoleWall", new Vector3(sx, y + 0.6f, zMid), new Vector3(0.3f, 1.2f, zLen + 0.3f));
            Concrete("Kerb", new Vector3(sx, y + KerbH * 0.5f, zMid), new Vector3(KerbW, KerbH, zLen));
            RailRun(new Vector3(sx, y + KerbH, zNear), new Vector3(sx, y + KerbH, zFar));

            // short return beside the ramp mouth (the hole is wider than the ramp)
            float xIn = (Arena.RampWidth * 0.5f + 0.1f) * side;
            float xOut = (x + KerbW * 0.5f) * side;
            float xMid = (xIn + xOut) * 0.5f, xLen = Mathf.Abs(xOut - xIn);
            Blocker("HoleWall", new Vector3(xMid, y + 0.6f, zNear), new Vector3(xLen, 1.2f, 0.3f));
            Concrete("Kerb", new Vector3(xMid, y + KerbH * 0.5f, zNear), new Vector3(xLen, KerbH, KerbW));
            RailRun(new Vector3(xIn, y + KerbH, zNear), new Vector3(xOut - KerbW * side, y + KerbH, zNear));
        }

        // bots must walk around the guarded hole (margin = agent radius + a little)
        const float m = 0.6f;
        Arena.HoleZ(f, out float zLo, out float zHi);
        if (s > 0f) NavPlanner.AddObstacle(f, -x - m, x + m, zLo - m, Arena.Half + 1f);
        else NavPlanner.AddObstacle(f, -x - m, x + m, -Arena.Half - 1f, zHi + m);
    }

    // A straight run of railing sections between two points (sections are
    // stretched slightly so a whole number always fits exactly).
    static void RailRun(Vector3 a, Vector3 b)
    {
        float len = Vector3.Distance(a, b);
        if (len < 0.3f) return;
        int count = Mathf.Max(1, Mathf.RoundToInt(len / ModuleLen));
        float each = len / count;
        Vector3 dir = (b - a) / len;
        Quaternion rot = Quaternion.LookRotation(Vector3.Cross(dir, Vector3.up), Vector3.up); // local X along the run
        var steel = Mats.Get("Steel");
        for (int i = 0; i < count; i++)
        {
            var go = Spawn("Railing", a + dir * (each * (i + 0.5f)), rot, root, _ => steel);
            go.transform.localScale = new Vector3(each / ModuleLen, 1f, 1f);
        }
    }

    // ------------------------------------------------------------------ ramps

    // The walking surface runs from deck i at the foot (|z| = RampFootZ) up to
    // deck i+1 at the edge of the hole (|z| = HoleZMin). The foot is buried a
    // little into the lower slab and the top tucks under the upper one, so
    // there are no gaps or lips.
    static void BuildRamp(int i)
    {
        float s = Arena.Side(i);
        const float thick = 0.4f, bury = 0.3f;
        Vector3 foot = new Vector3(0f, Arena.FloorY(i), Arena.RampFootZ * s);
        Vector3 top = new Vector3(0f, Arena.FloorY(i + 1), Arena.HoleZMin * s);
        Vector3 along = (top - foot).normalized;
        float length = Vector3.Distance(foot, top) + bury;
        Quaternion rot = Quaternion.LookRotation(along, Vector3.up);
        Vector3 normal = rot * Vector3.up;
        Vector3 center = (foot + top) * 0.5f - along * (bury * 0.5f) - normal * (thick * 0.5f);

        var ramp = new GameObject("Ramp");
        ramp.transform.SetParent(root, false);
        ramp.transform.SetPositionAndRotation(center, rot);

        var size = new Vector3(Arena.RampWidth, thick, length);
        Box("RampDeck", Vector3.zero, size, asphaltMat, wallMat, FloorTile, WallTile, true, ramp.transform);

        // low kerbs along both edges
        const float kw = 0.22f, kh = 0.14f;
        for (int side = -1; side <= 1; side += 2)
            Box("RampKerb", new Vector3((Arena.RampWidth * 0.5f - kw * 0.5f) * side, thick * 0.5f + kh * 0.5f, 0f),
                new Vector3(kw, kh, length - bury * 2f), wallMat, wallMat, WallTile, WallTile, false, ramp.transform);
        // (kerbs stop short of both ends so they do not poke through the slabs)

        // The low end has no headroom underneath: bots on deck i must go around it.
        // (From about 5 m in from the foot you can walk under the ramp.)
        const float m = 0.55f;
        float w = Arena.RampWidth * 0.5f + m;
        float zA = (Arena.RampFootZ - 5.2f) * s, zB = (Arena.RampFootZ + 0.3f) * s;
        NavPlanner.AddObstacle(i, -w, w, Mathf.Min(zA, zB), Mathf.Max(zA, zB));
    }

    // ------------------------------------------------------------------ columns & beams

    static void BuildStructure()
    {
        var concrete = Mats.Tiled("ConcreteWall", 1f / WallTile);
        var yellow = Mats.Get("HazardYellow");
        var black = Mats.Get("HazardBlack");
        Material ColumnMat(string slot) => slot == "HazardA" ? yellow : slot == "HazardB" ? black : concrete;

        const float m = 0.45f + Arena.ColumnRadius;     // nav margin around a column
        float clear = Arena.FloorHeight - Arena.SlabThickness;

        for (int f = 0; f < Arena.FloorCount - 1; f++)
        {
            float y = Arena.FloorY(f);
            foreach (float x in Arena.Grid)
                foreach (float z in Arena.Grid)
                {
                    if (Arena.InHole(f, x, z, 0.6f) || Arena.InHole(f + 1, x, z, 0.4f) || Arena.OnRampFootprint(f, x, z, 0.6f))
                        continue;
                    var col = Spawn("Column", new Vector3(x, y, z), Quaternion.identity, root, ColumnMat);
                    var cap = col.AddComponent<CapsuleCollider>();
                    cap.radius = Arena.ColumnRadius + 0.03f;
                    cap.height = clear;
                    cap.center = new Vector3(0f, clear * 0.5f, 0f);
                    if (Mathf.Abs(x) < 12f && Mathf.Abs(z) < 12f)
                        NavPlanner.AddObstacle(f, x - m, x + m, z - m, z + m);
                }
        }

        // downstand beams under every upper slab, on the column grid
        for (int f = 1; f < Arena.FloorCount; f++)
        {
            float under = Arena.FloorY(f) - Arena.SlabThickness;
            float h = Arena.Half - 0.01f;
            Arena.HoleZ(f, out float zLo, out float zHi);

            foreach (float x in Arena.Grid)    // beams running along Z never cross the hole
                Concrete("Beam", new Vector3(x, under - 0.21f, 0f), new Vector3(0.45f, 0.42f, h * 2f));

            foreach (float z in Arena.Grid)    // beams running along X are interrupted by it
            {
                bool cut = z > zLo - 0.3f && z < zHi + 0.3f;
                if (!cut)
                {
                    Concrete("Beam", new Vector3(0f, under - 0.18f, z), new Vector3(h * 2f, 0.36f, 0.45f));
                    continue;
                }
                float edge = Arena.HoleXHalf, len = h - edge;
                Concrete("Beam", new Vector3(-(edge + len * 0.5f), under - 0.18f, z), new Vector3(len, 0.36f, 0.45f));
                Concrete("Beam", new Vector3(edge + len * 0.5f, under - 0.18f, z), new Vector3(len, 0.36f, 0.45f));
            }

            // trimmers framing the long edges of the hole
            float zc = (zLo + zHi) * 0.5f, zl = zHi - zLo;
            Concrete("Beam", new Vector3(-(Arena.HoleXHalf + 0.16f), under - 0.15f, zc), new Vector3(0.32f, 0.3f, zl));
            Concrete("Beam", new Vector3(Arena.HoleXHalf + 0.16f, under - 0.15f, zc), new Vector3(0.32f, 0.3f, zl));
        }
    }

    // ------------------------------------------------------------------ floor paint

    // Painted parking bays along both side walls and a stop line at each ramp:
    // cheap detail that makes the decks read as a real car park and gives the
    // eye a sense of scale and speed.
    static void BuildMarkings()
    {
        var white = Mats.Get("PaintWhite");
        var yellow = Mats.Get("PaintYellow");
        const float lift = 0.004f, thick = 0.008f;     // a hair above the slab: no z-fighting

        void Line(Vector3 center, Vector3 size, Material m) =>
            Box("Paint", center, size, m, m, 1f, 1f, false);

        for (int f = 0; f < Arena.FloorCount; f++)
        {
            float y = Arena.FloorY(f) + lift;
            for (int side = -1; side <= 1; side += 2)
            {
                float xMid = 9.9f * side;
                for (float z = -11.25f; z <= 11.3f; z += 2.5f)
                {
                    // the goal occupies the bays in its corner of the ground deck
                    if (f == 0 && side > 0 && z < Arena.GoalPos.z + 3.4f) continue;
                    Line(new Vector3(xMid, y, z), new Vector3(5f, thick, 0.11f), white);
                }
            }

            if (f < Arena.FloorCount - 1)   // stop line across the foot of the ramp going up
                Line(new Vector3(0f, y, (Arena.RampFootZ + 0.35f) * Arena.Side(f)), new Vector3(Arena.RampWidth, thick, 0.2f), yellow);
            if (f > 0)                      // and where the ramp from below arrives
                Line(new Vector3(0f, y, (Arena.HoleZMin - 0.35f) * Arena.Side(f - 1)), new Vector3(Arena.RampWidth, thick, 0.2f), yellow);
        }
    }

    // ------------------------------------------------------------------ lamps

    public const float LampIntensity = 11f;

    /// <summary>Every ceiling lamp's light, and the glowing material they share.</summary>
    public static readonly System.Collections.Generic.List<Light> LampLights = new System.Collections.Generic.List<Light>();
    public static Material LampGlow { get; private set; }

    static void BuildLamps()
    {
        var body = Mats.Get("LampBody");
        // this scene's own copy: the lighting rig dims and flickers it
        var glow = LampGlow = new Material(Mats.Get("LampGlow"));
        LampLights.Clear();
        float[] grid = { -9f, 0f, 9f };

        for (int f = 0; f < Arena.FloorCount - 1; f++)
        {
            float ceiling = Arena.FloorY(f + 1) - Arena.SlabThickness;
            int n = 0;
            foreach (float x in grid)
                foreach (float z in grid)
                {
                    if (Arena.InHole(f + 1, x, z, 1.2f)) continue;
                    var rot = Quaternion.Euler(0f, (n++ % 2) * 90f, 0f);
                    var lamp = Spawn("Lamp", new Vector3(x, ceiling, z), rot, root, slot => slot == "LampGlow" ? glow : body);

                    // a wide downward cone, like a real batten: lights the deck, not the soffit
                    var lightGO = new GameObject("Light");
                    lightGO.transform.SetParent(lamp.transform, false);
                    lightGO.transform.localPosition = new Vector3(0f, -0.14f, 0f);
                    lightGO.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                    var l = lightGO.AddComponent<Light>();
                    l.type = LightType.Spot;
                    l.spotAngle = 156f;
                    l.innerSpotAngle = 100f;
                    l.color = new Color(1f, 0.93f, 0.82f);
                    l.intensity = LampIntensity;
                    l.range = 11f;
                    l.shadows = LightShadows.None;
                    LampLights.Add(l);
                }
        }
    }

    // ------------------------------------------------------------------ props

    struct PropDef { public string name; public float halfLen, height; }

    // Dressing along the two side walls, outside the area bots ever walk in.
    // Seeded, so the layout is the same every run.
    static void BuildProps()
    {
        var rng = new System.Random(20260614);
        var defs = new[]
        {
            new PropDef { name = "BarrelRed", halfLen = 0.33f, height = 0.88f },
            new PropDef { name = "BarrelBlue", halfLen = 0.36f, height = 0.93f },
            new PropDef { name = "Barrier", halfLen = 0.85f, height = 0.83f },
            new PropDef { name = "Crate", halfLen = 0.32f, height = 0.254f },
            new PropDef { name = "Box", halfLen = 0.31f, height = 0.34f },
        };
        int[] weights = { 3, 3, 2, 3, 3 };
        var parent = new GameObject("Props").transform;
        parent.SetParent(root, false);

        for (int f = 0; f < Arena.FloorCount; f++)
        {
            float y = Arena.FloorY(f);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = 12.2f * side;
                float z = -10.4f;
                while (z < 10.4f)
                {
                    z += 0.5f + (float)rng.NextDouble() * 3.2f;
                    var d = defs[Pick(rng, weights)];
                    float z0 = z, z1 = z + d.halfLen * 2f;
                    z = z1;
                    if (z1 > 10.6f) break;
                    if (NearColumnLine(z0 - 0.5f, z1 + 0.5f)) continue;
                    // keep the goal area clear
                    if (f == 0 && side > 0 && z0 < Arena.GoalPos.z + 4f) continue;

                    float zc = (z0 + z1) * 0.5f;
                    float yaw = d.name == "Barrier" ? 90f + Rand(rng, -4f, 4f) : Rand(rng, 0f, 360f);
                    float inset = d.name == "Barrier" ? 0f : Rand(rng, -0.12f, 0.12f);
                    Place(d.name, new Vector3(x + inset, y, zc), yaw, parent);

                    // sometimes stack a second crate / box on top
                    if ((d.name == "Crate" || d.name == "Box") && rng.NextDouble() < 0.45)
                        Place(d.name, new Vector3(x + inset + Rand(rng, -0.04f, 0.04f), y + d.height, zc + Rand(rng, -0.04f, 0.04f)),
                            yaw + Rand(rng, -25f, 25f), parent);
                }
            }
        }
    }

    static bool NearColumnLine(float z0, float z1)
    {
        foreach (float g in Arena.Grid)
            if (g > z0 - 0.45f && g < z1 + 0.45f) return true;
        return false;
    }

    static float Rand(System.Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);

    static int Pick(System.Random rng, int[] weights)
    {
        int total = 0;
        foreach (int w in weights) total += w;
        int r = rng.Next(total);
        for (int i = 0; i < weights.Length; i++)
        {
            if (r < weights[i]) return i;
            r -= weights[i];
        }
        return 0;
    }

    static void Place(string name, Vector3 pos, float yaw, Transform parent)
    {
        var mat = Mats.Get("Prop_" + name);
        var go = Spawn("Props/" + name, pos, Quaternion.Euler(0f, yaw, 0f), parent, _ => mat);
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            var bc = mf.gameObject.AddComponent<BoxCollider>();
            bc.center = mf.sharedMesh.bounds.center;
            bc.size = mf.sharedMesh.bounds.size;
        }
    }
}

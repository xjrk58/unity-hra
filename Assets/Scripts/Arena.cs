using UnityEngine;

/// <summary>
/// Shared description of the multi-storey arena geometry. The builder
/// (<see cref="ArenaBuilder"/>), the bootstrap and the AI navigation all read
/// from here so the world and the path-finding always agree.
///
/// Layout: <see cref="FloorCount"/> square concrete decks stacked
/// <see cref="FloorHeight"/> apart. Each upper deck has a rectangular hole that
/// spans the WHOLE ramp underneath it, so a climber has full head clearance the
/// entire way up and steps out onto the deck near the centre. Ramps hug
/// alternating edges (zig-zag) so they don't overlap.
/// </summary>
public static class Arena
{
    public const int FloorCount = 4;
    public const float FloorHeight = 5f;
    public const float Half = 13f;          // half-size of each square floor
    public const float SlabThickness = 0.5f;

    // Ramp: hugs an edge at |z| = RampFootZ (bottom) and rises toward the centre
    // at |z| = RampTopZ (top). The sign comes from Side(i).
    public const float RampFootZ = 11f;
    public const float RampTopZ = 1f;
    public const float RampWidth = 5f;

    // Hole in the upper floor: spans the full ramp run (plus margin) so there's
    // never a low-clearance pinch. X is wide enough to walk through comfortably.
    public const float HoleXHalf = 3.5f;
    public const float HoleZMin = 1.5f;     // near the centre (just past the ramp top)
    public const float HoleZMax = 12.5f;    // out toward the hugged edge

    // Structural grid: columns stand on every crossing of these lines.
    public static readonly float[] Grid = { -12.2f, -5.8f, 5.8f, 12.2f };
    public const float ColumnRadius = 0.3f;

    /// <summary>Where competitors deliver their farts (ground floor).</summary>
    public static readonly Vector3 GoalPos = new Vector3(9f, 0f, -9f);

    public static float FloorY(int floor) => floor * FloorHeight;

    /// <summary>Floor of something hovering near a deck (pickups, the goal).</summary>
    public static int FloorOf(float y) =>
        Mathf.Clamp(Mathf.RoundToInt(y / FloorHeight), 0, FloorCount - 1);

    /// <summary>Deck a pair of feet is standing on; while on a ramp this is the lower deck.</summary>
    public static int FloorOfFeet(float feetY) =>
        Mathf.Clamp(Mathf.FloorToInt((feetY + 0.6f) / FloorHeight), 0, FloorCount - 1);

    /// <summary>Side (+1 / -1 in Z) that ramp <paramref name="i"/> hugs.</summary>
    public static float Side(int i) => (i % 2 == 0) ? 1f : -1f;

    /// <summary>Foot of ramp i (near the edge), flush with floor i.</summary>
    public static Vector3 RampBottom(int i) => new Vector3(0f, FloorY(i), RampFootZ * Side(i));

    /// <summary>Top of ramp i (near the centre), flush with floor i+1.</summary>
    public static Vector3 RampTop(int i) => new Vector3(0f, FloorY(i + 1), RampTopZ * Side(i));

    /// <summary>Spot on floor i just beyond the foot of ramp i: where you line up to climb, or step off after descending.</summary>
    public static Vector3 RampFootExit(int i) => new Vector3(0f, FloorY(i), (RampFootZ + 1f) * Side(i));

    /// <summary>Spot on floor i+1 just past the top of ramp i: where you step off after climbing, or line up to descend.</summary>
    public static Vector3 RampTopExit(int i) => new Vector3(0f, FloorY(i + 1), (RampTopZ - 0.8f) * Side(i));

    /// <summary>Z extent of the hole in floor <paramref name="f"/> (f ≥ 1), ordered low to high.</summary>
    public static void HoleZ(int f, out float zLo, out float zHi)
    {
        float s = Side(f - 1);
        zLo = Mathf.Min(HoleZMin * s, HoleZMax * s);
        zHi = Mathf.Max(HoleZMin * s, HoleZMax * s);
    }

    /// <summary>True if (x, z) lies in the hole of floor f, grown by <paramref name="margin"/>.</summary>
    public static bool InHole(int f, float x, float z, float margin = 0f)
    {
        if (f <= 0 || f >= FloorCount) return false;
        HoleZ(f, out float zLo, out float zHi);
        return Mathf.Abs(x) <= HoleXHalf + margin && z >= zLo - margin && z <= zHi + margin;
    }

    /// <summary>True if (x, z) on floor f is under/inside the low part of the ramp that climbs from it.</summary>
    public static bool OnRampFootprint(int f, float x, float z, float margin = 0f)
    {
        if (f < 0 || f >= FloorCount - 1) return false;
        float zs = z * Side(f);
        return Mathf.Abs(x) <= RampWidth * 0.5f + margin && zs >= RampTopZ - margin && zs <= RampFootZ + margin;
    }
}

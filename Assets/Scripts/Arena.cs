using UnityEngine;

/// <summary>
/// Shared description of the multi-storey arena geometry. Both the builder
/// (<see cref="GameBootstrap"/>) and the AI navigation read from here so the
/// world and the path-finding always agree.
///
/// Layout: <see cref="FloorCount"/> square platforms stacked
/// <see cref="FloorHeight"/> apart. Each upper floor has a rectangular hole that
/// spans the WHOLE ramp underneath it, so a climber has full head clearance the
/// entire way up and steps out onto the large floor area near the centre. Ramps
/// hug alternating edges (zig-zag) so they don't overlap.
/// </summary>
public static class Arena
{
    public const int FloorCount = 4;
    public const float FloorHeight = 5f;
    public const float Half = 13f;        // half-size of each square floor

    // Ramp: hugs an edge at |z| = RampFootZ (bottom) and rises toward the centre
    // at |z| = RampTopZ (top). The sign comes from Side(i).
    public const float RampFootZ = 11f;
    public const float RampTopZ = 1f;
    public const float RampWidth = 5f;

    // Hole in the upper floor: spans the full ramp run (plus margin) so there's
    // never a low-clearance pinch. X is wide enough to walk through comfortably.
    public const float HoleXHalf = 3.5f;
    public const float HoleZMin = 1.5f;   // near the centre (just past the ramp top)
    public const float HoleZMax = 12.5f;  // out toward the hugged edge

    public static float FloorY(int floor) => floor * FloorHeight;

    public static int FloorOf(float y) =>
        Mathf.Clamp(Mathf.RoundToInt(y / FloorHeight), 0, FloorCount - 1);

    /// <summary>Side (+1 / -1 in Z) that ramp <paramref name="i"/> hugs.</summary>
    public static float Side(int i) => (i % 2 == 0) ? 1f : -1f;

    /// <summary>Foot of ramp i (near the edge), flush with floor i.</summary>
    public static Vector3 RampBottom(int i) => new Vector3(0f, FloorY(i), RampFootZ * Side(i));

    /// <summary>Top of ramp i (near the centre), flush with floor i+1.</summary>
    public static Vector3 RampTop(int i) => new Vector3(0f, FloorY(i + 1), RampTopZ * Side(i));
}

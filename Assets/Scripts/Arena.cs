using UnityEngine;

/// <summary>
/// Shared description of the multi-storey arena geometry. Both the builder
/// (<see cref="GameBootstrap"/>) and the AI navigation read from here so the
/// world and the path-finding always agree.
///
/// Layout: <see cref="FloorCount"/> square platforms stacked
/// <see cref="FloorHeight"/> apart. Each upper floor has a rectangular hole
/// through which a ramp rises from the floor below. Ramps alternate sides
/// (zig-zag) so they don't overlap.
/// </summary>
public static class Arena
{
    public const int FloorCount = 4;
    public const float FloorHeight = 5f;
    public const float Half = 13f;        // half-size of each square floor

    public const float HoleXHalf = 3f;    // hole half-width in X
    public const float HoleZMin = 6f;     // hole spans Z in [HoleZMin, HoleZMax] (times Side)
    public const float HoleZMax = 11f;
    public const float RampTopZ = HoleZMax; // ramp meets the upper floor here

    public static float FloorY(int floor) => floor * FloorHeight;

    public static int FloorOf(float y) =>
        Mathf.Clamp(Mathf.RoundToInt(y / FloorHeight), 0, FloorCount - 1);

    /// <summary>Side (+1 / -1 in Z) that ramp <paramref name="i"/> rises toward.</summary>
    public static float Side(int i) => (i % 2 == 0) ? 1f : -1f;

    /// <summary>Foot of ramp i, flush with floor i.</summary>
    public static Vector3 RampBottom(int i) => new Vector3(0f, FloorY(i), 0f);

    /// <summary>Top of ramp i, flush with floor i+1 (at its hole).</summary>
    public static Vector3 RampTop(int i) => new Vector3(0f, FloorY(i + 1), RampTopZ * Side(i));
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tiny per-floor path planner for the bots. Each deck has a handful of
/// rectangular no-go areas (the stairwell hole with its railings, the low end of
/// the ramp, columns). Paths are found on the visibility graph of the rectangle
/// corners, which is exact for this kind of layout and only a few dozen nodes.
/// </summary>
public static class NavPlanner
{
    const float Bound = 12.1f;      // bots never need to walk closer to the edge than this
    const float CornerPad = 0.18f;  // waypoints sit slightly outside the rectangle corners

    static readonly List<Rect>[] obstacles = NewLists();
    static readonly List<Vector2> nodes = new List<Vector2>(64);
    static float[] dist = new float[64];
    static int[] prev = new int[64];
    static bool[] done = new bool[64];

    static List<Rect>[] NewLists()
    {
        var l = new List<Rect>[Arena.FloorCount];
        for (int i = 0; i < l.Length; i++) l[i] = new List<Rect>();
        return l;
    }

    public static void Clear()
    {
        foreach (var l in obstacles) l.Clear();
    }

    /// <summary>Registers a no-go rectangle (already grown by the agent radius) on a floor.</summary>
    public static void AddObstacle(int floor, float xMin, float xMax, float zMin, float zMax)
    {
        if (floor < 0 || floor >= obstacles.Length) return;
        obstacles[floor].Add(Rect.MinMaxRect(xMin, zMin, xMax, zMax));
    }

    public static IReadOnlyList<Rect> Obstacles(int floor) => obstacles[floor];

    /// <summary>True if a straight walk between two points on a floor is unobstructed.</summary>
    public static bool Clear(int floor, Vector3 from, Vector3 to) =>
        Clear(new Vector2(from.x, from.z), new Vector2(to.x, to.z), obstacles[floor]);

    /// <summary>Next point to walk toward on the way from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public static Vector3 Next(int floor, Vector3 from, Vector3 to)
    {
        var obs = obstacles[floor];
        var a = new Vector2(from.x, from.z);
        var b = new Vector2(to.x, to.z);

        // standing inside a no-go margin (pushed there, or just stepped off a ramp): walk straight out first
        for (int i = 0; i < obs.Count; i++)
            if (Inside(obs[i], a)) return At(Escape(obs[i], a), to.y);

        for (int i = 0; i < obs.Count; i++)
            if (Inside(obs[i], b)) b = Escape(obs[i], b);

        if (Clear(a, b, obs)) return At(b, to.y);

        // visibility graph: start, goal, and every usable rectangle corner
        nodes.Clear();
        nodes.Add(a);
        nodes.Add(b);
        for (int i = 0; i < obs.Count; i++)
        {
            Rect r = obs[i];
            AddCorner(new Vector2(r.xMin - CornerPad, r.yMin - CornerPad), obs);
            AddCorner(new Vector2(r.xMax + CornerPad, r.yMin - CornerPad), obs);
            AddCorner(new Vector2(r.xMin - CornerPad, r.yMax + CornerPad), obs);
            AddCorner(new Vector2(r.xMax + CornerPad, r.yMax + CornerPad), obs);
        }

        int n = nodes.Count;
        if (dist.Length < n) { dist = new float[n * 2]; prev = new int[n * 2]; done = new bool[n * 2]; }
        for (int i = 0; i < n; i++) { dist[i] = float.MaxValue; prev[i] = -1; done[i] = false; }
        dist[0] = 0f;

        while (true)
        {
            int u = -1;
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
                if (!done[i] && dist[i] < best) { best = dist[i]; u = i; }
            if (u < 0 || u == 1) break;
            done[u] = true;
            for (int v = 0; v < n; v++)
            {
                if (done[v]) continue;
                float d = dist[u] + Vector2.Distance(nodes[u], nodes[v]);
                if (d >= dist[v]) continue;
                if (!Clear(nodes[u], nodes[v], obs)) continue;
                dist[v] = d;
                prev[v] = u;
            }
        }

        if (prev[1] < 0) return At(b, to.y);     // no route known: best effort
        int hop = 1;
        while (prev[hop] != 0) hop = prev[hop];
        return At(nodes[hop], to.y);
    }

    static void AddCorner(Vector2 p, List<Rect> obs)
    {
        if (Mathf.Abs(p.x) > Bound || Mathf.Abs(p.y) > Bound) return;
        for (int i = 0; i < obs.Count; i++)
            if (Inside(obs[i], p)) return;
        nodes.Add(p);
    }

    static Vector3 At(Vector2 p, float y) => new Vector3(p.x, y, p.y);

    static bool Inside(Rect r, Vector2 p) =>
        p.x > r.xMin && p.x < r.xMax && p.y > r.yMin && p.y < r.yMax;

    // Nearest point just outside the rectangle that is still on the deck.
    static Vector2 Escape(Rect r, Vector2 p)
    {
        Vector2 best = p;
        float bestD = float.MaxValue;
        Try(new Vector2(r.xMin - CornerPad, p.y), p, ref best, ref bestD);
        Try(new Vector2(r.xMax + CornerPad, p.y), p, ref best, ref bestD);
        Try(new Vector2(p.x, r.yMin - CornerPad), p, ref best, ref bestD);
        Try(new Vector2(p.x, r.yMax + CornerPad), p, ref best, ref bestD);
        return best;
    }

    static void Try(Vector2 c, Vector2 p, ref Vector2 best, ref float bestD)
    {
        if (Mathf.Abs(c.x) > Bound || Mathf.Abs(c.y) > Bound) return;
        float d = (c - p).sqrMagnitude;
        if (d < bestD) { bestD = d; best = c; }
    }

    static bool Clear(Vector2 a, Vector2 b, List<Rect> obs)
    {
        for (int i = 0; i < obs.Count; i++)
            if (SegmentHits(a, b, obs[i])) return false;
        return true;
    }

    // Slab test: does segment a→b pass through the open interior of r?
    static bool SegmentHits(Vector2 a, Vector2 b, Rect r)
    {
        const float eps = 0.02f;
        float t0 = 0f, t1 = 1f;
        Vector2 d = b - a;
        if (!Slab(a.x, d.x, r.xMin + eps, r.xMax - eps, ref t0, ref t1)) return false;
        if (!Slab(a.y, d.y, r.yMin + eps, r.yMax - eps, ref t0, ref t1)) return false;
        return t0 < t1;
    }

    static bool Slab(float o, float d, float lo, float hi, ref float t0, ref float t1)
    {
        if (Mathf.Abs(d) < 1e-6f) return o > lo && o < hi;
        float ta = (lo - o) / d, tb = (hi - o) / d;
        if (ta > tb) { float t = ta; ta = tb; tb = t; }
        if (ta > t0) t0 = ta;
        if (tb < t1) t1 = tb;
        return t0 < t1;
    }
}

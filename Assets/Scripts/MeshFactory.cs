using UnityEngine;

/// <summary>
/// Procedural meshes with texture coordinates in real-world units, so tiling
/// PBR textures keep a constant texel density on every slab, beam and kerb no
/// matter how large it is.
/// </summary>
public static class MeshFactory
{
    /// <summary>
    /// Axis-aligned box centred on the origin. Sub-mesh 0 is the top face,
    /// sub-mesh 1 everything else, so a deck can have a floor finish on top and
    /// raw concrete on its sides and soffit.
    /// </summary>
    /// <param name="size">Full extents in metres.</param>
    /// <param name="uvOrigin">World position of the box centre; makes neighbouring boxes share one continuous texture.</param>
    /// <param name="topTile">Metres covered by one repeat of the top texture.</param>
    /// <param name="sideTile">Metres covered by one repeat of the side texture.</param>
    public static Mesh Box(Vector3 size, Vector3 uvOrigin, float topTile, float sideTile)
    {
        Vector3 h = size * 0.5f;
        var v = new Vector3[24];
        var n = new Vector3[24];
        var uv = new Vector2[24];
        int k = 0;

        // each face: normal, then two in-plane axes (u, v) chosen so the winding faces outward
        Face(Vector3.up, Vector3.right, Vector3.forward);
        Face(Vector3.down, Vector3.right, Vector3.back);
        Face(Vector3.right, Vector3.forward, Vector3.up);
        Face(Vector3.left, Vector3.back, Vector3.up);
        Face(Vector3.forward, Vector3.left, Vector3.up);
        Face(Vector3.back, Vector3.right, Vector3.up);

        void Face(Vector3 normal, Vector3 uAxis, Vector3 vAxis)
        {
            float tile = normal == Vector3.up ? topTile : sideTile;
            Vector3 c = Vector3.Scale(normal, h);
            Vector3 du = Vector3.Scale(uAxis, h), dv = Vector3.Scale(vAxis, h);
            Vector3[] corners = { c - du - dv, c - du + dv, c + du + dv, c + du - dv };
            for (int i = 0; i < 4; i++)
            {
                Vector3 world = corners[i] + uvOrigin;
                v[k] = corners[i];
                n[k] = normal;
                uv[k] = new Vector2(Vector3.Dot(world, uAxis), Vector3.Dot(world, vAxis)) / tile;
                k++;
            }
        }

        var mesh = new Mesh { name = "Box" };
        mesh.vertices = v;
        mesh.normals = n;
        mesh.uv = uv;
        mesh.subMeshCount = 2;
        mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
        var rest = new int[30];
        for (int f = 1; f < 6; f++)
        {
            int b = f * 4, o = (f - 1) * 6;
            rest[o] = b; rest[o + 1] = b + 1; rest[o + 2] = b + 2;
            rest[o + 3] = b; rest[o + 4] = b + 2; rest[o + 5] = b + 3;
        }
        mesh.SetTriangles(rest, 1);
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>Flat upward-facing quad centred on the origin, UVs in metres / tile.</summary>
    public static Mesh Ground(float size, float tile)
    {
        float h = size * 0.5f;
        var mesh = new Mesh { name = "Ground" };
        mesh.vertices = new[] { new Vector3(-h, 0, -h), new Vector3(-h, 0, h), new Vector3(h, 0, h), new Vector3(h, 0, -h) };
        mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        mesh.uv = new[] { new Vector2(-h, -h) / tile, new Vector2(-h, h) / tile, new Vector2(h, h) / tile, new Vector2(h, -h) / tile };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }
}

using UnityEngine;

/// <summary>
/// Helpers for creating runtime materials with the built-in Standard shader,
/// so the whole game can be generated procedurally without authored assets.
/// </summary>
public static class MaterialUtil
{
    static Shader Standard => Shader.Find("Standard");

    public static Material Opaque(Color color)
    {
        var m = new Material(Standard) { color = color };
        return m;
    }

    public static Material Emissive(Color color, float intensity = 1f)
    {
        var m = new Material(Standard) { color = color };
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", color * intensity);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        return m;
    }

    public static Material Transparent(Color color)
    {
        var m = new Material(Standard);
        m.SetFloat("_Mode", 3f);
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.DisableKeyword("_ALPHATEST_ON");
        m.EnableKeyword("_ALPHABLEND_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = 3000;
        m.color = color;
        return m;
    }
}

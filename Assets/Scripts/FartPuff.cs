using UnityEngine;

/// <summary>
/// Short-lived expanding, fading translucent sphere spawned whenever a fart is
/// expelled. Purely cosmetic.
/// </summary>
public class FartPuff : MonoBehaviour
{
    const float MaxLife = 0.5f;
    const float StartScale = 0.3f;
    const float EndScale = 1.7f;

    float life;
    Material mat;
    Color baseColor;

    public static void Spawn(Vector3 position, Color tint)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "FartPuff";
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);

        go.transform.position = position + Vector3.up * 0.6f;
        go.transform.localScale = Vector3.one * StartScale;

        var puff = go.AddComponent<FartPuff>();
        var c = Color.Lerp(tint, new Color(0.6f, 0.85f, 0.2f), 0.6f);
        c.a = 0.6f;
        puff.baseColor = c;
        puff.mat = MaterialUtil.Transparent(c);
        go.GetComponent<MeshRenderer>().material = puff.mat;
    }

    void Update()
    {
        life += Time.deltaTime;
        float t = life / MaxLife;
        if (t >= 1f) { Destroy(gameObject); return; }

        transform.localScale = Vector3.one * Mathf.Lerp(StartScale, EndScale, t);
        transform.position += Vector3.up * (0.6f * Time.deltaTime);

        var c = baseColor;
        c.a = Mathf.Lerp(0.6f, 0f, t);
        mat.color = c;
    }
}

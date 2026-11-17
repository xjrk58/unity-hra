using UnityEngine;

/// <summary>
/// A collectible "fart": a hovering, glowing puff of green gas inside a swirl
/// of wisps, with a faint beam of light above it so it can be spotted from the
/// other end of a deck. Picked up on trigger overlap by any competitor whose
/// backpack isn't full, then hides and respawns after a delay.
/// </summary>
[RequireComponent(typeof(Collider))]
public class FartPickup : MonoBehaviour
{
    public float respawnDelay = 4f;
    public bool Available { get; private set; } = true;

    /// <summary>
    /// On a device that joined somebody else's game the farts are only shown:
    /// the host decides who picked what up and says so (<see cref="NetSetAvailable"/>).
    /// </summary>
    public static bool DisplayOnly;

    /// <summary>Where it hovers (it bobs around this point).</summary>
    public Vector3 Home => home;

    /// <summary>How many farts it puts into a backpack (a rainbow fart is worth three).</summary>
    public int worth = 1;

    /// <summary>True for the fart a horse or unicorn leaves (it is not one of the scattered ones).</summary>
    public bool IsAnimalDrop => animalDrop;

    bool animalDrop, rainbow;

    ParticleSystem wisp;
    Transform core;
    LineRenderer beam;
    Collider col;
    Vector3 home;
    float bobPhase;

    void Awake()
    {
        col = GetComponent<Collider>();
        home = transform.position;
        bobPhase = Random.value * Mathf.PI * 2f;
        wisp = GasFx.Wisp(transform);
        BuildCore();
        BuildBeam();
        if (GameManager.Instance != null) GameManager.Instance.RegisterPickup(this);
    }

    // The solid heart of the puff: a little cloud of glowing lobes. Unlike the wisps it
    // is opaque and emissive, so it reads in full sunlight as well as in the dark.
    void BuildCore()
    {
        core = new GameObject("Core").transform;
        core.SetParent(transform, false);
        var mat = Mats.Get("Gas");
        Lobe(mat, new Vector3(0f, 0f, 0f), 0.46f);
        Lobe(mat, new Vector3(0.2f, -0.06f, 0.05f), 0.33f);
        Lobe(mat, new Vector3(-0.2f, -0.04f, -0.06f), 0.31f);
        Lobe(mat, new Vector3(0.03f, 0.17f, 0.02f), 0.29f);
        Lobe(mat, new Vector3(-0.02f, -0.05f, 0.19f), 0.27f);
    }

    void Lobe(Material mat, Vector3 at, float diameter)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        DestroyImmediate(go.GetComponent<Collider>());
        go.name = "Lobe";
        go.transform.SetParent(core, false);
        go.transform.localPosition = at;
        go.transform.localScale = Vector3.one * diameter;
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    // A soft column of green light standing on the puff.
    void BuildBeam()
    {
        var go = new GameObject("Beam");
        go.transform.SetParent(transform, false);
        beam = go.AddComponent<LineRenderer>();
        beam.useWorldSpace = false;
        beam.positionCount = 2;
        beam.SetPosition(0, new Vector3(0f, -1.0f, 0f));
        beam.SetPosition(1, new Vector3(0f, 3.4f, 0f));
        beam.widthMultiplier = 0.55f;
        beam.alignment = LineAlignment.View;
        beam.textureMode = LineTextureMode.Stretch;
        beam.sharedMaterial = Mats.Get("GasPuff");
        beam.startColor = beam.endColor = new Color(0.45f, 0.95f, 0.2f, 0.5f);
        beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        beam.receiveShadows = false;
    }

    void Update()
    {
        if (!Available) return;
        float t = Time.time * 2f + bobPhase;
        transform.position = home + Vector3.up * (Mathf.Sin(t) * 0.12f);
        core.localRotation = Quaternion.Euler(0f, t * 28f, 0f);
        core.localScale = Vector3.one * (1f + 0.09f * Mathf.Sin(t * 1.7f));
    }

    void ShowSolid(bool on)
    {
        core.gameObject.SetActive(on);
        beam.enabled = on;
    }

    void OnTriggerEnter(Collider other) => TryCollect(other);

    // also checked while overlapping, so someone who empties their backpack
    // while standing in the wisp still picks it up
    void OnTriggerStay(Collider other) => TryCollect(other);

    void TryCollect(Collider other)
    {
        if (DisplayOnly) return;
        TryCollectBy(other.GetComponent<Competitor>());
    }

    /// <summary>Somebody is touching it: into their backpack it goes, if there is room.</summary>
    public bool TryCollectBy(Competitor c)
    {
        if (DisplayOnly || !Available) return false;
        if (c == null || c.BackpackFull || c.Stunned || c.Gone) return false;
        if (!c.Collect(worth)) return false;
        Consume(rainbow ? Color.white : c.Tint);
        if (!animalDrop) Invoke(nameof(Respawn), respawnDelay);     // an animal's fart comes back when the animal farts again
        return true;
    }

    void Consume(Color tint)
    {
        Available = false;
        col.enabled = false;
        ShowSolid(false);
        if (wisp != null)
        {
            wisp.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            GasFx.Burst(transform.position, tint, 0.6f);
        }
    }

    /// <summary>
    /// Turns this into the fart an animal leaves behind: not there until the
    /// animal farts, never back by itself, and - from a unicorn - a rainbow
    /// worth three.
    /// </summary>
    public void MakeAnimalDrop(bool fromUnicorn)
    {
        animalDrop = true;
        rainbow = fromUnicorn;
        worth = fromUnicorn ? 3 : 1;
        if (rainbow)
        {
            // every lobe its own colour, a rainbow column of light, rainbow wisps
            var block = new MaterialPropertyBlock();
            int i = 0;
            foreach (var r in core.GetComponentsInChildren<MeshRenderer>())
            {
                Color c = GasFx.RainbowColor(i++);
                block.SetColor("_BaseColor", c);
                block.SetColor("_EmissionColor", c.linear * 2.6f);
                r.SetPropertyBlock(block);
            }
            beam.colorGradient = GasFx.RainbowGradient(0.55f);
            if (wisp != null) Destroy(wisp.gameObject);
            wisp = GasFx.Wisp(transform, true);
        }
        Available = false;
        col.enabled = false;
        ShowSolid(false);
        if (wisp != null) wisp.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    /// <summary>The animal has farted: its fart now hangs here.</summary>
    public void Drop(Vector3 at)
    {
        home = at;
        transform.position = at;
        if (!Available) Respawn();
    }

    /// <summary>Joining device: where the host's animal left it.</summary>
    public void NetMove(Vector3 at)
    {
        if (at == home) return;
        home = at;
        transform.position = at;
    }

    /// <summary>Joining device: the host says whether this one is there to be taken.</summary>
    public void NetSetAvailable(bool there)
    {
        if (there == Available) return;
        if (there) Respawn();
        else Consume(Color.white);
    }

    void Respawn()
    {
        Available = true;
        col.enabled = true;
        ShowSolid(true);
        if (wisp != null) wisp.Play(true);
    }
}

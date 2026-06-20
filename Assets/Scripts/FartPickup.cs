using UnityEngine;

/// <summary>
/// A collectible "fart" orb. Picked up on trigger overlap by any competitor
/// whose backpack isn't full, then hides and respawns after a delay.
/// </summary>
[RequireComponent(typeof(Collider))]
public class FartPickup : MonoBehaviour
{
    public float respawnDelay = 4f;
    public bool Available { get; private set; } = true;

    Renderer rend;
    Collider col;
    Vector3 home;
    float bobPhase;

    void Awake()
    {
        rend = GetComponentInChildren<Renderer>();
        col = GetComponent<Collider>();
        home = transform.position;
        bobPhase = Random.value * Mathf.PI * 2f;
        if (GameManager.Instance != null) GameManager.Instance.RegisterPickup(this);
    }

    void Update()
    {
        if (!Available) return;
        float y = Mathf.Sin(Time.time * 2f + bobPhase) * 0.15f;
        transform.position = home + Vector3.up * y;
        transform.Rotate(Vector3.up, 60f * Time.deltaTime, Space.World);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!Available) return;
        var c = other.GetComponent<Competitor>();
        if (c == null || c.BackpackFull) return;
        if (c.Collect(1)) Consume();
    }

    void Consume()
    {
        Available = false;
        if (rend != null) rend.enabled = false;
        if (col != null) col.enabled = false;
        Invoke(nameof(Respawn), respawnDelay);
    }

    void Respawn()
    {
        Available = true;
        if (rend != null) rend.enabled = true;
        if (col != null) col.enabled = true;
    }
}

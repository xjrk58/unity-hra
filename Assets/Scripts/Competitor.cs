using UnityEngine;

/// <summary>
/// Shared state and behaviour for anyone playing the game (the human player and
/// the AI bots). Each competitor collects "farts" into a backpack of limited
/// capacity and later expels them at the goal to score.
/// </summary>
public abstract class Competitor : MonoBehaviour
{
    public string DisplayName = "Hráč";
    public Color Tint = Color.green;
    public int Capacity = 8;

    public int Backpack { get; protected set; }
    public int Score { get; protected set; }

    /// <summary>Set by <see cref="GoalZone"/> while this competitor stands in the goal.</summary>
    public bool InGoal { get; set; }

    public bool BackpackFull => Backpack >= Capacity;

    public virtual bool IsPlayer => false;

    float stunUntil;

    /// <summary>True while this competitor is temporarily knocked out by a fart blast.</summary>
    public bool Stunned => Time.time < stunUntil;

    /// <summary>Try to add farts to the backpack. Returns false if already full.</summary>
    public bool Collect(int amount = 1)
    {
        if (BackpackFull) return false;
        Backpack = Mathf.Min(Capacity, Backpack + amount);
        return true;
    }

    /// <summary>Spend farts from the backpack (e.g. to fire a blast). False if not enough.</summary>
    public bool SpendFromBackpack(int amount)
    {
        if (Backpack < amount) return false;
        Backpack -= amount;
        return true;
    }

    /// <summary>
    /// Temporarily knock this competitor out: hide it, switch off its collider and
    /// stop it acting for <paramref name="seconds"/>, then bring it back.
    /// </summary>
    public void Stun(float seconds)
    {
        stunUntil = Time.time + seconds;
        InGoal = false;
        FartPuff.Spawn(transform.position, new Color(0.6f, 0.85f, 0.2f));
        SetActiveVisuals(false);
        CancelInvoke(nameof(EndStun));
        Invoke(nameof(EndStun), seconds);
    }

    void EndStun()
    {
        SetActiveVisuals(true);
    }

    void SetActiveVisuals(bool on)
    {
        foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = on;
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = on; // disabled => others can pass through, AI can't move
    }

    /// <summary>
    /// Expel a single fart from the backpack into the score, with a puff effect.
    /// Returns false if the backpack is empty.
    /// </summary>
    public bool Expel()
    {
        if (Backpack <= 0) return false;
        Backpack--;
        Score++;
        FartPuff.Spawn(transform.position, Tint);
        if (GameManager.Instance != null) GameManager.Instance.OnScoreChanged(this);
        return true;
    }
}

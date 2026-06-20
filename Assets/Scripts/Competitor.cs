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

    /// <summary>Try to add farts to the backpack. Returns false if already full.</summary>
    public bool Collect(int amount = 1)
    {
        if (BackpackFull) return false;
        Backpack = Mathf.Min(Capacity, Backpack + amount);
        return true;
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

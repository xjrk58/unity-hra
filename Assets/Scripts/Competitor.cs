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

    /// <summary>True for people (as opposed to bots). The monster prefers them.</summary>
    public virtual bool IsHuman => IsPlayer;

    /// <summary>Roughly where the backpack is: gas comes out here, blasts are aimed here.</summary>
    public Vector3 ChestPosition => transform.position + Vector3.up * 0.25f;

    float stunUntil;
    bool stunned;

    /// <summary>True while this competitor is temporarily knocked out by a fart blast.</summary>
    public bool Stunned => stunned;

    /// <summary>Seconds of knock-out left (0 when not stunned).</summary>
    public float StunRemaining => Mathf.Max(0f, stunUntil - Time.time);

    /// <summary>What knocked this competitor out last (null = a rival's blast, "monster" = the fart monster).</summary>
    public string StunCause { get; private set; }

    /// <summary>True once this competitor has left the game (the other player hung up).</summary>
    public bool Gone { get; private set; }

    /// <summary>
    /// Counts this competitor's blasts and remembers the last victim (an index into
    /// the roster, or <see cref="NetGame.MonsterIndex"/>): how a blast is shown on the other device.
    /// </summary>
    public int BlastSeq { get; private set; }
    public int BlastTarget { get; private set; }

    /// <summary>Try to add farts to the backpack. Returns false if already full.</summary>
    public bool Collect(int amount = 1)
    {
        if (BackpackFull) return false;
        Backpack = Mathf.Min(Capacity, Backpack + amount);
        FartAudio.Pickup(transform.position, IsPlayer);
        return true;
    }

    /// <summary>Spend farts from the backpack (e.g. to fire a blast). False if not enough.</summary>
    public bool SpendFromBackpack(int amount)
    {
        if (Backpack < amount) return false;
        Backpack -= amount;
        return true;
    }

    /// <summary>Everything in the backpack is gone (the monster ate it). Returns how much that was.</summary>
    public int LoseBackpack()
    {
        int lost = Backpack;
        Backpack = 0;
        return lost;
    }

    /// <summary>
    /// Fire a fart blast at the monster: costs <paramref name="cost"/> farts and
    /// drives it away for a while. Returns false if it can't be paid for.
    /// </summary>
    public bool BlastMonster(FartMonster monster, int cost)
    {
        if (monster == null || !monster.CanBeBlasted) return false;
        if (!SpendFromBackpack(cost)) return false;
        GasFx.Jet(ChestPosition, monster.ChestPosition, Tint);
        FartAudio.Blast(ChestPosition, IsPlayer);
        monster.Repel();
        BlastSeq = (BlastSeq + 1) & 0xff;
        BlastTarget = NetGame.MonsterIndex;
        return true;
    }

    /// <summary>
    /// Fire a fart blast at a rival: costs <paramref name="cost"/> farts and knocks
    /// the target out for <paramref name="seconds"/>. Returns false if it can't be paid for.
    /// </summary>
    public bool BlastRival(Competitor target, int cost, float seconds)
    {
        if (target == null || target == this || target.Stunned || target.Gone) return false;
        if (!SpendFromBackpack(cost)) return false;
        Vector3 from = ChestPosition, to = target.ChestPosition;
        GasFx.Jet(from, to, Tint);
        FartAudio.Blast(from, IsPlayer);
        target.Stun(seconds);
        BlastSeq = (BlastSeq + 1) & 0xff;
        BlastTarget = GameManager.Instance != null ? Mathf.Max(0, GameManager.Instance.IndexOf(target)) : 0;
        return true;
    }

    /// <summary>
    /// Temporarily knock this competitor out: it vanishes into a lingering gas
    /// cloud, its collider is switched off and it cannot act for
    /// <paramref name="seconds"/>, then it comes back.
    /// </summary>
    /// <paramref name="linger"/> keeps the knock-out going that much longer than its
    /// countdown shows (the joining device waits for the host to say it is over).
    public void Stun(float seconds, string cause = null, float linger = 0f)
    {
        StunCause = cause;
        stunUntil = Time.time + seconds;
        stunned = true;
        InGoal = false;
        GasFx.Burst(ChestPosition, Tint, 2.2f);
        GasFx.Linger(transform.position, seconds);
        SetActiveVisuals(false);
        CancelInvoke(nameof(EndStun));
        Invoke(nameof(EndStun), seconds + linger);
        OnStunned(seconds);
    }

    protected virtual void OnStunned(float seconds) { }

    void EndStun()
    {
        stunned = false;        // only now: the controller is back on, so moving is safe again
        SetActiveVisuals(true);
        GasFx.Burst(ChestPosition, Tint, 1.4f);
    }

    void SetActiveVisuals(bool on)
    {
        foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = on;
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = on; // disabled => others can pass through, AI can't move
    }

    /// <summary>The other player hung up: this competitor disappears and takes no further part.</summary>
    public void Leave()
    {
        if (Gone) return;
        Gone = true;
        InGoal = false;
        CancelInvoke(nameof(EndStun));
        stunned = false;
        GasFx.Burst(ChestPosition, Tint, 2f);
        SetActiveVisuals(false);
    }

    /// <summary>
    /// Joining device: take over what the host says about this competitor, with the
    /// sounds and the gas for whatever changed since the last report.
    /// </summary>
    public void NetApply(int backpack, int score, bool isStunned, float stunLeft, string cause, bool inGoal, int blastSeq, int blastTarget)
    {
        if (Gone) return;
        if (score > Score)
        {
            GasFx.Burst(transform.position + Vector3.up * -0.1f - transform.forward * 0.35f, Tint, 1f);
            FartAudio.Expel(transform.position, IsPlayer);
        }
        else if (backpack > Backpack) FartAudio.Pickup(transform.position, IsPlayer);
        Backpack = backpack;
        Score = score;
        if (!IsPlayer) InGoal = inGoal;     // (this device's own player feels the goal itself)

        if (isStunned && !stunned) Stun(stunLeft, cause, 0.6f);
        else if (!isStunned && stunned)
        {
            CancelInvoke(nameof(EndStun));
            EndStun();
        }

        if (blastSeq != BlastSeq)
        {
            BlastSeq = blastSeq;
            BlastTarget = blastTarget;
            Vector3 to = ChestPosition + transform.forward * 6f;
            var gm = GameManager.Instance;
            if (blastTarget == NetGame.MonsterIndex) { if (FartMonster.Instance != null) to = FartMonster.Instance.ChestPosition; }
            else if (gm != null && blastTarget < gm.Competitors.Count) to = gm.Competitors[blastTarget].ChestPosition;
            GasFx.Jet(ChestPosition, to, Tint);
            FartAudio.Blast(ChestPosition, IsPlayer);
        }
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
        GasFx.Burst(transform.position + Vector3.up * -0.1f - transform.forward * 0.35f, Tint, 1f);
        FartAudio.Expel(transform.position, IsPlayer);
        if (GameManager.Instance != null) GameManager.Instance.OnScoreChanged(this);
        return true;
    }
}

using UnityEngine;

/// <summary>
/// A competitor steered from the other device. On the host this is the joining
/// player; on the joining device it is everybody the host simulates (the host's
/// player and the bots). It glides after the positions it is sent, so the
/// walking animation (which follows real movement) stays smooth.
/// </summary>
public class NetPuppet : Competitor
{
    /// <summary>True for the other person, false for a bot shown on the joining device.</summary>
    public bool human;

    public override bool IsHuman => human;

    Vector3 goal;
    float goalYaw;
    bool hasGoal;
    CharacterController cc;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
    }

    /// <summary>Where the other device says this competitor is now.</summary>
    public void MoveTo(Vector3 position, float yaw)
    {
        if (!hasGoal || (position - transform.position).sqrMagnitude > 16f) Place(position, yaw);   // too far to glide: jump
        goal = position;
        goalYaw = yaw;
        hasGoal = true;
    }

    void Place(Vector3 position, float yaw)
    {
        bool on = cc != null && cc.enabled;
        if (on) cc.enabled = false;
        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        if (on) cc.enabled = true;
    }

    void Update()
    {
        if (!hasGoal || Gone) return;
        float k = 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime);
        transform.SetPositionAndRotation(Vector3.Lerp(transform.position, goal, k),
            Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, goalYaw, 0f), k));

        // On the host the joining player picks farts up and stands in the goal simply by
        // being there (it is moved, not walked, so the trigger volumes are not relied on).
        if (!Session.IsHost || !human) return;
        InGoal = InGoalBox(transform.position);
        if (Stunned || BackpackFull) return;
        var gm = GameManager.Instance;
        if (gm == null || gm.Paused) return;
        foreach (var p in gm.Pickups)
            if (p != null && p.Available && (p.transform.position - transform.position).sqrMagnitude < 1.5f * 1.5f)
                p.TryCollectBy(this);
    }

    /// <summary>The goal's trigger box, as plain arithmetic.</summary>
    public static bool InGoalBox(Vector3 p) =>
        Mathf.Abs(p.x - Arena.GoalPos.x) <= 2.5f && Mathf.Abs(p.z - Arena.GoalPos.z) <= 2.5f &&
        p.y >= Arena.GoalPos.y && p.y <= Arena.GoalPos.y + 4f;
}

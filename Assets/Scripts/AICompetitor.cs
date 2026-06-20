using UnityEngine;

/// <summary>
/// Simple AI rival: wanders to the nearest available fart pickup until the
/// backpack is full (or pickups run out), then heads to the goal and expels.
/// Open-arena steering, no navmesh required.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class AICompetitor : Competitor
{
    public float moveSpeed = 5f;
    public float turnSpeed = 240f;
    public float expelInterval = 0.25f;

    enum State { Collect, Deliver }

    CharacterController cc;
    State state = State.Collect;
    FartPickup target;
    Transform goal;
    float expelTimer;
    float repathTimer;

    void Start()
    {
        cc = GetComponent<CharacterController>();
        if (GameManager.Instance != null) goal = GameManager.Instance.GoalTransform;
    }

    void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.GameOver) return;

        UpdateState();

        if (state == State.Collect) DoCollect();
        else DoDeliver();

        // keep grounded
        if (!cc.isGrounded) cc.Move(Vector3.down * 9.8f * Time.deltaTime);
    }

    void UpdateState()
    {
        if (BackpackFull) state = State.Deliver;
        else if (Backpack == 0) state = State.Collect;
        // if partially full we keep whatever state we're in until full/empty,
        // unless we've nothing left to collect (handled in DoCollect).
    }

    void DoCollect()
    {
        repathTimer -= Time.deltaTime;
        if (target == null || !target.Available || repathTimer <= 0f)
        {
            target = GameManager.Instance.NearestAvailablePickup(transform.position);
            repathTimer = 1.5f;
        }

        if (target != null)
        {
            MoveTowards(target.transform.position);
        }
        else if (Backpack > 0)
        {
            // nothing to collect right now — go cash in what we have
            state = State.Deliver;
        }
    }

    void DoDeliver()
    {
        if (goal == null) return;
        MoveTowards(goal.position);

        if (InGoal)
        {
            expelTimer -= Time.deltaTime;
            if (expelTimer <= 0f)
            {
                Expel();
                expelTimer = expelInterval;
            }
            if (Backpack == 0) state = State.Collect;
        }
    }

    void MoveTowards(Vector3 worldTarget)
    {
        Vector3 flat = worldTarget - transform.position;
        flat.y = 0f;
        float dist = flat.magnitude;
        if (dist < 0.1f) return;

        Vector3 dir = flat / dist;
        Quaternion want = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * Time.deltaTime);
        cc.Move(dir * moveSpeed * Time.deltaTime);
    }
}

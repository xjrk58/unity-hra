using UnityEngine;

/// <summary>
/// AI rival: collects the most convenient fart pickups until the backpack is
/// full (or pickups run out), carries them to the goal and expels them — and
/// now and then spends two farts to blast a rival it can see.
///
/// Navigation: on a deck it follows <see cref="NavPlanner"/> around the
/// stairwell holes, ramps and columns; between decks it lines up with a ramp
/// and commits to walking it end to end.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class AICompetitor : Competitor
{
    public float moveSpeed = 4.7f;          // the player walks at 6 and sprints at 8.6
    public float turnSpeed = 420f;
    public float expelInterval = 0.25f;

    [Header("Fart blast")]
    public int blastCost = 2;
    public float blastRange = 13f;
    public float blastStun = 5f;            // bots hit less hard than the player's 10 s

    enum State { Collect, Deliver }

    CharacterController cc;
    State state = State.Collect;
    FartPickup target;
    Transform goal;
    float expelTimer, repathTimer, verticalVel;

    // ramp traversal: once lined up, walk the whole ramp without re-planning
    int rampIndex = -1;
    bool rampUp;
    float rampTimer;

    // cached step from the planner
    Vector3 waypoint;
    float waypointTimer;

    // getting unstuck (other characters, the odd prop)
    Vector3 stuckAnchor;
    float stuckTimer, sidestepTimer;
    Vector3 sidestepDir;

    float blastTimer;

    void Start()
    {
        cc = GetComponent<CharacterController>();
        if (GameManager.Instance != null) goal = GameManager.Instance.GoalTransform;
        stuckAnchor = transform.position;
        blastTimer = Random.Range(14f, 24f);
    }

    void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.GameOver) return;
        if (Stunned) return; // knocked out by a fart blast — sit this one out

        UpdateState();

        Vector3? destination = null;
        if (state == State.Collect) destination = CollectDestination();
        if (state == State.Deliver && goal != null) destination = goal.position;

        if (destination.HasValue)
        {
            bool arrived = state == State.Deliver && InGoal && HDist(goal.position) < 1.6f;
            Step(arrived ? transform.position : Navigate(destination.Value));
        }
        else Step(transform.position);

        if (state == State.Deliver && InGoal) DoExpel();
        TryBlast();
    }

    void UpdateState()
    {
        if (BackpackFull) state = State.Deliver;
        else if (Backpack == 0) state = State.Collect;
        // partially full: keep doing whatever we were doing until full / empty,
        // unless there is nothing left to collect (handled in CollectDestination).
    }

    Vector3? CollectDestination()
    {
        repathTimer -= Time.deltaTime;
        if (target == null || !target.Available || repathTimer <= 0f)
        {
            target = GameManager.Instance.BestPickupFor(transform.position);
            repathTimer = 1.5f;
        }

        if (target != null)
        {
            // a decent haul and the next fart is a trek away: bank it rather than wander
            Vector3 d = target.transform.position - transform.position;
            float climb = Mathf.Abs(Arena.FloorOf(target.transform.position.y) - Arena.FloorOfFeet(FeetY));
            d.y = 0f;
            if (Backpack >= 6 && d.magnitude + 26f * climb > 13f)
            {
                state = State.Deliver;
                return null;
            }
            return target.transform.position;
        }
        if (Backpack > 0) state = State.Deliver;    // nothing to collect right now — go cash in what we have
        return null;
    }

    void DoExpel()
    {
        expelTimer -= Time.deltaTime;
        if (expelTimer <= 0f)
        {
            Expel();
            expelTimer = expelInterval;
        }
        if (Backpack == 0) state = State.Collect;
    }

    // ------------------------------------------------------------------ blasting

    void TryBlast()
    {
        blastTimer -= Time.deltaTime;
        if (blastTimer > 0f) return;
        blastTimer = 1f;                                    // re-check once a second
        if (Backpack < blastCost + 2 || InGoal) return;     // never blast away the whole haul

        Competitor best = null;
        float bestScore = 0f;
        foreach (var c in GameManager.Instance.Competitors)
        {
            if (c == null || c == this || c.Stunned || c.Gone) continue;
            Vector3 to = c.ChestPosition - ChestPosition;
            float dist = to.magnitude;
            if (dist > blastRange || dist < 1.5f) continue;
            if (Physics.Linecast(ChestPosition, c.ChestPosition, out var hit, ~0, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponent<Competitor>() != c) continue;   // something in the way
            // worth it? rivals who are ahead or carrying a lot are juicier targets
            float worth = 0.12f + 0.05f * c.Backpack + 0.035f * Mathf.Max(0, c.Score - Score);
            if (worth > bestScore) { bestScore = worth; best = c; }
        }

        if (best != null && Random.value < bestScore * 0.6f && BlastRival(best, blastCost, blastStun))
            blastTimer = Random.Range(20f, 34f);
    }

    // ------------------------------------------------------------------ navigation

    float FeetY => transform.position.y - cc.height * 0.5f;

    // Returns the next point to steer toward on the way to finalTarget.
    Vector3 Navigate(Vector3 finalTarget)
    {
        Vector3 pos = transform.position;

        // committed to a ramp: keep going until we step off the far end
        if (rampIndex >= 0)
        {
            rampTimer += Time.deltaTime;
            Vector3 exit = rampUp ? Arena.RampTopExit(rampIndex) : Arena.RampFootExit(rampIndex);
            if (HDist(exit) < 0.5f || rampTimer > 9f) rampIndex = -1;
            else return exit;
        }

        int cf = Arena.FloorOfFeet(FeetY);
        int tf = Arena.FloorOf(finalTarget.y);

        // somehow standing part-way up a ramp without being committed to it: finish it
        if (cc.isGrounded && FeetY > Arena.FloorY(cf) + 0.3f && cf < Arena.FloorCount - 1
            && Arena.OnRampFootprint(cf, pos.x, pos.z, 0.2f))
        {
            BeginRamp(cf, tf > cf);
            return rampUp ? Arena.RampTopExit(cf) : Arena.RampFootExit(cf);
        }

        if (cf == tf) return Plan(cf, finalTarget);

        if (tf > cf)
        {
            Vector3 lineUp = Arena.RampFootExit(cf);
            if (HDist(lineUp) < 0.6f) { BeginRamp(cf, true); return Arena.RampTopExit(cf); }
            return Plan(cf, lineUp);
        }
        else
        {
            Vector3 lineUp = Arena.RampTopExit(cf - 1);
            if (HDist(lineUp) < 0.6f) { BeginRamp(cf - 1, false); return Arena.RampFootExit(cf - 1); }
            return Plan(cf, lineUp);
        }
    }

    void BeginRamp(int index, bool up)
    {
        rampIndex = index;
        rampUp = up;
        rampTimer = 0f;
        waypointTimer = 0f;
    }

    // Planner results are reused for a moment; the route only changes slowly.
    Vector3 Plan(int floor, Vector3 to)
    {
        waypointTimer -= Time.deltaTime;
        if (waypointTimer <= 0f || HDist(waypoint) < 0.35f)
        {
            waypoint = NavPlanner.Next(floor, transform.position, to);
            waypointTimer = 0.2f;
        }
        return waypoint;
    }

    float HDist(Vector3 p)
    {
        Vector3 d = p - transform.position;
        d.y = 0f;
        return d.magnitude;
    }

    // Moves one frame toward worldTarget (or just settles on the ground if already there).
    void Step(Vector3 worldTarget)
    {
        float dt = Time.deltaTime;
        Vector3 flat = worldTarget - transform.position;
        flat.y = 0f;
        float dist = flat.magnitude;
        Vector3 move = Vector3.zero;

        if (dist > 0.12f)
        {
            Vector3 dir = flat / dist;

            // stuck against something the planner doesn't know about? shuffle sideways for a moment
            if (sidestepTimer > 0f)
            {
                sidestepTimer -= dt;
                dir = (dir * 0.35f + sidestepDir).normalized;
            }
            stuckTimer += dt;
            if (stuckTimer > 0.7f)
            {
                Vector3 moved = transform.position - stuckAnchor;
                moved.y = 0f;
                if (moved.magnitude < 0.35f)
                {
                    sidestepDir = Vector3.Cross(Vector3.up, dir) * (Random.value < 0.5f ? 1f : -1f);
                    sidestepTimer = 0.55f;
                    waypointTimer = 0f;
                }
                stuckAnchor = transform.position;
                stuckTimer = 0f;
            }

            Quaternion want = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * dt);
            // ease off while still turning toward the target, and when about to arrive
            float facing = Mathf.Clamp01(Vector3.Dot(transform.forward, dir) * 1.5f - 0.2f);
            float speed = moveSpeed * Mathf.Lerp(0.35f, 1f, facing) * Mathf.Clamp01(dist / 0.4f);
            move = dir * speed;
        }
        else
        {
            stuckAnchor = transform.position;
            stuckTimer = 0f;
        }

        if (cc.isGrounded) verticalVel = -5f;       // enough to stay glued to a ramp on the way down
        else verticalVel -= 20f * dt;
        cc.Move((move + Vector3.up * verticalVel) * dt);
    }
}

using UnityEngine;

/// <summary>
/// First-person controller for the human player: movement, sprint, looking
/// around, jumping, expelling farts while standing in the goal and blasting a
/// rival. The controls themselves (keyboard and mouse, or touch) come from
/// <see cref="GameInput"/>.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : Competitor
{
    public float moveSpeed = 6f;
    public float sprintSpeed = 8.6f;
    public float jumpSpeed = 6f;
    public float gravity = 20f;
    public float expelInterval = 0.12f;

    [Header("Fart blast")]
    public int blastCost = 2;        // farts spent per blast
    public float stunDuration = 10f; // how long a hit rival is gone
    public float blastRange = 40f;

    const float EyeHeight = 0.7f;    // above the controller centre
    const float BaseFov = 68f;

    CharacterController cc;
    Camera cam;
    float pitch;
    float verticalVel;
    float expelCooldown;
    float bobPhase, bobAmount, landDip;
    bool wasGrounded = true;

    public override bool IsPlayer => true;

    /// <summary>The rival currently under the crosshair and within blast range (null if none).</summary>
    public Competitor AimTarget { get; private set; }

    /// <summary>True when the fart monster is under the crosshair (and nothing solid is in the way).</summary>
    public bool AimMonster { get; private set; }

    /// <summary>True when a blast fired right now would hit and can be paid for.</summary>
    public bool CanBlast => (AimTarget != null || AimMonster) && Backpack >= blastCost;

    // the jump scare: for a moment the view is dragged onto the monster's face
    Transform scareFocus;
    float scareUntil;

    /// <summary>True while the monster has hold of the view.</summary>
    public bool Scared => Time.time < scareUntil && scareFocus != null;

    /// <summary>Caught: the camera snaps to <paramref name="focus"/> and shakes; nothing else works meanwhile.</summary>
    public void Scare(Transform focus, float seconds)
    {
        scareFocus = focus;
        scareUntil = Time.time + seconds;
    }

    void Start()
    {
        cc = GetComponent<CharacterController>();
        cam = GetComponentInChildren<Camera>();
        DisplayName = "Ty";
        if (!GameInput.Touch)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.GameOver)
            return;

        if (Scared)
        {
            AimTarget = null;
            AimMonster = false;
            ScareLook();
            return;
        }

        Look();
        if (Stunned)
        {
            AimTarget = null;
            AimMonster = false;
            return;                 // knocked out: you can look around, nothing else
        }

        Move();
        UpdateAim();
        HandleExpel();
        HandleBlast();
    }

    void UpdateAim()
    {
        AimTarget = null;
        AimMonster = false;
        if (cam == null) return;
        var ray = new Ray(cam.transform.position, cam.transform.forward);
        float reach = blastRange;
        Competitor target = null;
        if (Physics.Raycast(ray, out var hit, blastRange, ~0, QueryTriggerInteraction.Ignore))
        {
            reach = hit.distance;
            target = hit.collider.GetComponent<Competitor>();
        }

        // the monster is gas, with no collider of its own: it is "hit" when the ray passes
        // through it before reaching anything solid
        var monster = FartMonster.Instance;
        if (monster != null && monster.CanBeBlasted && monster.RayHits(ray, reach)) { AimMonster = true; return; }
        if (target != null && target != this && !target.Stunned) AimTarget = target;
    }

    void HandleBlast()
    {
        if (!GameInput.BlastDown) return;
        if (!CanBlast) return;
        if (Session.IsClient)
        {
            // joined somebody else's game: ask the host to do it
            if (AimMonster) NetGame.SendAction(NetGame.ActBlastMonster);
            else NetGame.SendAction(NetGame.ActBlastRival, GameManager.Instance.IndexOf(AimTarget));
            return;
        }
        if (AimMonster) BlastMonster(FartMonster.Instance, blastCost);
        else BlastRival(AimTarget, blastCost, stunDuration);
    }

    // Caught by the monster: the head is wrenched round to its face, the view shakes and narrows.
    void ScareLook()
    {
        if (cam == null) return;
        Vector3 to = scareFocus.position - cam.transform.position;
        Vector3 flat = new Vector3(to.x, 0f, to.z);
        if (flat.sqrMagnitude > 0.0001f)
        {
            float yaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            float y = Mathf.MoveTowardsAngle(transform.eulerAngles.y, yaw, 900f * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0f, y, 0f);
            float want = -Mathf.Atan2(to.y, flat.magnitude) * Mathf.Rad2Deg;
            pitch = Mathf.MoveTowards(pitch, Mathf.Clamp(want, -85f, 85f), 600f * Time.deltaTime);
        }
        Vector3 jolt = Random.insideUnitSphere * 0.045f;
        cam.transform.localPosition = new Vector3(jolt.x, EyeHeight + jolt.y, jolt.z);
        cam.transform.localEulerAngles = new Vector3(pitch + Random.Range(-1.2f, 1.2f), Random.Range(-1.2f, 1.2f), Random.Range(-2.5f, 2.5f));
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, BaseFov - 14f, 1f - Mathf.Exp(-14f * Time.deltaTime));
    }

    void Look()
    {
        Vector2 look = GameInput.Look;
        transform.Rotate(Vector3.up, look.x, Space.Self);
        pitch = Mathf.Clamp(pitch - look.y, -85f, 85f);
        if (cam != null) cam.transform.localEulerAngles = new Vector3(pitch, 0f, 0f);
    }

    void Move()
    {
        Vector2 move = GameInput.Move;
        Vector3 dir = transform.right * move.x + transform.forward * move.y;
        if (dir.sqrMagnitude > 1f) dir.Normalize();

        bool sprinting = GameInput.Sprint && move.y > 0.1f;
        float speed = sprinting ? sprintSpeed : moveSpeed;

        bool grounded = cc.isGrounded;
        if (grounded)
        {
            if (!wasGrounded) landDip = Mathf.Clamp(-verticalVel * 0.012f, 0f, 0.12f);
            verticalVel = -4f;      // keeps the feet on ramps when walking down
            if (GameInput.JumpDown) verticalVel = jumpSpeed;
        }
        else
        {
            verticalVel -= gravity * Time.deltaTime;
        }
        wasGrounded = grounded;

        Vector3 velocity = dir * speed + Vector3.up * verticalVel;
        cc.Move(velocity * Time.deltaTime);

        AnimateCamera(grounded ? dir.magnitude * speed : 0f, sprinting);
    }

    // Subtle head bob, a dip on landing and a slightly wider view when sprinting.
    void AnimateCamera(float groundSpeed, bool sprinting)
    {
        if (cam == null) return;
        float dt = Time.deltaTime;
        bobAmount = Mathf.Lerp(bobAmount, Mathf.Clamp01(groundSpeed / moveSpeed), 1f - Mathf.Exp(-10f * dt));
        bobPhase += groundSpeed * 1.75f * dt;
        landDip = Mathf.Lerp(landDip, 0f, 1f - Mathf.Exp(-9f * dt));

        float y = EyeHeight + Mathf.Abs(Mathf.Cos(bobPhase)) * 0.045f * bobAmount - 0.02f * bobAmount - landDip;
        float x = Mathf.Sin(bobPhase) * 0.02f * bobAmount;
        cam.transform.localPosition = new Vector3(x, y, 0f);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, sprinting ? BaseFov + 7f : BaseFov, 1f - Mathf.Exp(-8f * dt));
    }

    void HandleExpel()
    {
        expelCooldown -= Time.deltaTime;
        if (InGoal && GameInput.ExpelHeld && expelCooldown <= 0f && Backpack > 0)
        {
            if (Session.IsClient)
            {
                NetGame.SendAction(NetGame.ActExpel);       // the host keeps the score
                expelCooldown = expelInterval;
            }
            else if (Expel()) expelCooldown = expelInterval;
        }
    }
}

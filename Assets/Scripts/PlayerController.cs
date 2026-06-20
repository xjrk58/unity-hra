using UnityEngine;

/// <summary>
/// First-person controller for the human player: WASD movement, mouse look,
/// jumping, and holding Fire1 to expel farts while standing in the goal.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : Competitor
{
    public float moveSpeed = 6f;
    public float lookSensitivity = 2f;
    public float jumpSpeed = 6f;
    public float gravity = 20f;
    public float expelInterval = 0.12f;

    [Header("Fart blast")]
    public int blastCost = 2;        // farts spent per blast
    public float stunDuration = 10f; // how long a hit rival is gone
    public float blastRange = 40f;

    CharacterController cc;
    Camera cam;
    float pitch;
    float verticalVel;
    float expelCooldown;

    public override bool IsPlayer => true;

    void Start()
    {
        cc = GetComponent<CharacterController>();
        cam = GetComponentInChildren<Camera>();
        DisplayName = "Ty";
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.GameOver)
            return;

        Look();
        Move();
        HandleExpel();
        HandleBlast();
    }

    void HandleBlast()
    {
        if (!Input.GetButtonDown("Fire2")) return;
        if (Backpack < blastCost) return;
        if (cam == null) return;

        var ray = new Ray(cam.transform.position, cam.transform.forward);
        if (!Physics.Raycast(ray, out var hit, blastRange)) return;

        var target = hit.collider.GetComponent<Competitor>();
        if (target == null || target == this || target.Stunned) return;

        // hit a rival: pay the cost and knock them out
        SpendFromBackpack(blastCost);
        FartPuff.Spawn(hit.point, Tint);
        target.Stun(stunDuration);
    }

    void Look()
    {
        float mouseX = Input.GetAxis("Mouse X") * lookSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * lookSensitivity;
        transform.Rotate(Vector3.up, mouseX, Space.Self);
        pitch = Mathf.Clamp(pitch - mouseY, -85f, 85f);
        if (cam != null) cam.transform.localEulerAngles = new Vector3(pitch, 0f, 0f);
    }

    void Move()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 dir = transform.right * h + transform.forward * v;
        if (dir.sqrMagnitude > 1f) dir.Normalize();

        if (cc.isGrounded)
        {
            verticalVel = -1f;
            if (Input.GetButtonDown("Jump")) verticalVel = jumpSpeed;
        }
        else
        {
            verticalVel -= gravity * Time.deltaTime;
        }

        Vector3 velocity = dir * moveSpeed + Vector3.up * verticalVel;
        cc.Move(velocity * Time.deltaTime);
    }

    void HandleExpel()
    {
        expelCooldown -= Time.deltaTime;
        if (InGoal && Input.GetButton("Fire1") && expelCooldown <= 0f)
        {
            if (Expel()) expelCooldown = expelInterval;
        }
    }
}

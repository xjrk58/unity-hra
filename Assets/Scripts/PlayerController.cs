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

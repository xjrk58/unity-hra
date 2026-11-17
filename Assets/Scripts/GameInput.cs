using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The one place the game reads its controls from. On a computer that is the
/// keyboard and mouse; on a phone it is the touch screen:
///
///   left part of the screen   – a floating stick: move (pushed to the rim = sprint)
///   right part of the screen  – drag to look around
///   SKOK / PRD! buttons       – jump / blast the rival under the crosshair
///   in the goal               – farts are expelled automatically
///   after the match           – tap for a new game; the system Back button quits
///
/// Every control claims the finger that started on it and keeps it until that
/// finger lifts, so moving, looking and pressing a button work at the same time.
/// </summary>
[DefaultExecutionOrder(-200)]   // touch state is ready before anything reads it
public class GameInput : MonoBehaviour
{
    /// <summary>One finger on the screen, in screen pixels (origin bottom-left).</summary>
    public struct Finger
    {
        public int id;
        public Vector2 position;
        public Finger(int id, Vector2 position) { this.id = id; this.position = position; }
    }

    /// <summary>A round on-screen button, in screen pixels.</summary>
    public struct Zone
    {
        public Vector2 centre;
        public float radius;
        public bool Contains(Vector2 p) => radius > 0f && (p - centre).sqrMagnitude <= radius * radius;
    }

    // ------------------------------------------------------------------ mode

    static bool? touchMode;

    /// <summary>
    /// True when the game is driven by the touch screen. Phones and tablets
    /// always are; "-touchHud" on the command line forces it on a computer so
    /// the touch layout can be looked at and tested there.
    /// </summary>
    public static bool Touch
    {
        get
        {
            if (touchMode == null)
                touchMode = Application.isMobilePlatform || Array.IndexOf(Environment.GetCommandLineArgs(), "-touchHud") >= 0;
            return touchMode.Value;
        }
    }

    /// <summary>
    /// Test hook: when set, fingers come from here instead of the touch screen.
    /// </summary>
    public static Func<IReadOnlyList<Finger>> FingerSource;

    // ------------------------------------------------------------------ what the game reads

    /// <summary>Strafe (x) and forward (y), length at most 1.</summary>
    public static Vector2 Move =>
        Touch ? state.move : new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

    /// <summary>Turn right (x) and look up (y) this frame, in degrees.</summary>
    public static Vector2 Look =>
        Touch ? state.look : new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * MouseSensitivity;

    public static bool Sprint => Touch ? state.sprint : Input.GetKey(KeyCode.LeftShift);
    public static bool JumpDown => Touch ? state.jumpDown : Input.GetButtonDown("Jump");
    public static bool BlastDown => Touch ? state.blastDown : Input.GetButtonDown("Fire2");

    /// <summary>On a touch screen standing in the goal is enough.</summary>
    public static bool ExpelHeld => Touch || Input.GetButton("Fire1");

    /// <summary>"New game" after the match; a tap only counts once the banner has been up for a moment.</summary>
    public static bool RestartDown(float secondsSinceGameOver) =>
        Touch ? state.tapDown && secondsSinceGameOver > 1.5f : Input.GetKeyDown(KeyCode.R);

    /// <summary>Esc on a computer, the system Back button on a phone.</summary>
    public static bool QuitDown => Input.GetKeyDown(KeyCode.Escape);

    // ------------------------------------------------------------------ what the HUD reads and writes

    /// <summary>Where the buttons are on screen; the HUD keeps these up to date.</summary>
    public static Zone JumpZone, BlastZone;

    public static bool StickHeld => state.moveFinger != NoFinger;
    public static Vector2 StickOrigin => state.stickOrigin;
    public static Vector2 StickPosition => state.stickPosition;
    public static bool JumpHeld => state.jumpFinger != NoFinger;
    public static bool BlastHeld => state.blastFinger != NoFinger;

    /// <summary>How far the stick travels from its centre, in pixels.</summary>
    public static float StickRadius => Screen.height * 0.13f;

    // ------------------------------------------------------------------ tuning

    const float MouseSensitivity = 2f;
    const float MoveZoneWidth = 0.42f;      // the stick lives in this part of the screen (from the left)
    const float DeadZone = 0.14f;
    const float SprintAt = 0.9f;
    const float LookDegreesPerScreenHeight = 115f;
    const float LookPitchScale = 0.8f;
    const int NoFinger = int.MinValue;

    struct State
    {
        public Vector2 move, look, stickOrigin, stickPosition, lookLast;
        public bool sprint, jumpDown, blastDown, tapDown;
        public int moveFinger, lookFinger, jumpFinger, blastFinger;
    }

    static State state = NewState();
    static readonly List<Finger> fingers = new List<Finger>();
    static readonly HashSet<int> known = new HashSet<int>();
    static readonly List<int> gone = new List<int>();

    static State NewState() => new State
    {
        moveFinger = NoFinger, lookFinger = NoFinger, jumpFinger = NoFinger, blastFinger = NoFinger,
    };

    /// <summary>Called once per scene by the bootstrap.</summary>
    public static void Init()
    {
        state = NewState();
        known.Clear();
        JumpZone = BlastZone = default;
        if (!Touch) return;
        Input.simulateMouseWithTouches = false;     // a finger is not a mouse click
        Input.multiTouchEnabled = true;
        new GameObject("GameInput").AddComponent<GameInput>();
    }

    void Update() => Poll();

    /// <summary>Reads the fingers and works out this frame's controls.</summary>
    public static void Poll()
    {
        fingers.Clear();
        if (FingerSource != null)
        {
            fingers.AddRange(FingerSource());
        }
        else
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) continue;
                fingers.Add(new Finger(t.fingerId, t.position));
            }
        }

        state.jumpDown = state.blastDown = state.tapDown = false;
        state.look = Vector2.zero;

        // fingers that have left the screen give their control back
        gone.Clear();
        foreach (int id in known)
            if (!Has(id)) gone.Add(id);
        foreach (int id in gone)
        {
            known.Remove(id);
            if (state.moveFinger == id) state.moveFinger = NoFinger;
            if (state.lookFinger == id) state.lookFinger = NoFinger;
            if (state.jumpFinger == id) state.jumpFinger = NoFinger;
            if (state.blastFinger == id) state.blastFinger = NoFinger;
        }

        // a new finger takes the control it lands on: a button first, then stick or look
        foreach (var f in fingers)
        {
            if (!known.Add(f.id)) continue;
            state.tapDown = true;
            if (JumpZone.Contains(f.position))
            {
                if (state.jumpFinger == NoFinger) { state.jumpFinger = f.id; state.jumpDown = true; }
            }
            else if (BlastZone.Contains(f.position))
            {
                if (state.blastFinger == NoFinger) { state.blastFinger = f.id; state.blastDown = true; }
            }
            else if (f.position.x < Screen.width * MoveZoneWidth)
            {
                if (state.moveFinger == NoFinger)
                {
                    state.moveFinger = f.id;
                    state.stickOrigin = state.stickPosition = f.position;
                }
            }
            else if (state.lookFinger == NoFinger)
            {
                state.lookFinger = f.id;
                state.lookLast = f.position;
            }
        }

        // stick
        state.move = Vector2.zero;
        state.sprint = false;
        if (state.moveFinger != NoFinger && Find(state.moveFinger, out Vector2 stick))
        {
            float radius = Mathf.Max(1f, StickRadius);
            Vector2 offset = stick - state.stickOrigin;
            if (offset.magnitude > radius)
            {
                // the stick follows a thumb that slides past its rim
                state.stickOrigin = stick - offset.normalized * radius;
                offset = stick - state.stickOrigin;
            }
            state.stickPosition = stick;
            Vector2 v = offset / radius;
            float m = v.magnitude;
            if (m > DeadZone)
            {
                state.move = v / m * Mathf.InverseLerp(DeadZone, SprintAt, m);
                state.sprint = m >= SprintAt;
            }
        }

        // look
        if (state.lookFinger != NoFinger && Find(state.lookFinger, out Vector2 look))
        {
            Vector2 delta = (look - state.lookLast) / Mathf.Max(1f, Screen.height) * LookDegreesPerScreenHeight;
            state.look = new Vector2(delta.x, delta.y * LookPitchScale);
            state.lookLast = look;
        }
    }

    static bool Has(int id)
    {
        foreach (var f in fingers)
            if (f.id == id) return true;
        return false;
    }

    static bool Find(int id, out Vector2 position)
    {
        foreach (var f in fingers)
            if (f.id == id) { position = f.position; return true; }
        position = default;
        return false;
    }
}

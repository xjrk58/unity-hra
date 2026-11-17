using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Dev tool: plays the game with scripted fingers to check the touch controls
/// without a phone.
///
///   Unity -batchmode -projectPath . -executeMethod TouchTestRunner.Run -touchHud
///
/// Fingers are fed in through <see cref="GameInput.FingerSource"/>, so everything
/// after the touch screen itself is the real game: the stick walks and sprints,
/// a second finger turns and tilts the view while the first keeps walking, the
/// SKOK and PRD! buttons work, standing in the goal expels by itself and a tap
/// starts a new game. Logs "[TouchTest] PASS" or "[TouchTest] FAIL". Do NOT pass -quit.
/// </summary>
[InitializeOnLoad]
public static class TouchTestRunner
{
    const string ActiveKey = "TouchTestRunner.Active";

    static readonly List<GameInput.Finger> fingers = new List<GameInput.Finger>();
    static readonly List<string> failures = new List<string>();
    static IEnumerator<object> script;
    static int lastFrame = -1, checks, errors;

    static TouchTestRunner()
    {
        if (!SessionState.GetBool(ActiveKey, false)) return;
        FartMonster.Enabled = false;        // this test is about the controls
        FartAnimal.Enabled = false;         // (and a fart dropped at the player's feet would spoil the sums)
        Application.logMessageReceived += (msg, stack, type) =>
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) errors++;
        };
        EditorApplication.update += Tick;
    }

    public static void Run()
    {
        if (!GameInput.Touch)
        {
            Debug.LogError("[TouchTest] FAIL: start Unity with -touchHud");
            EditorApplication.Exit(2);
            return;
        }
        SessionState.SetBool(ActiveKey, true);
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        EditorApplication.isPlaying = true;
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        if (script == null) script = Script();

        bool more;
        try { more = script.MoveNext(); }
        catch (Exception e) { failures.Add("exception in the test: " + e); more = false; }
        if (more) return;

        EditorApplication.update -= Tick;
        SessionState.SetBool(ActiveKey, false);
        if (errors > 0) failures.Add(errors + " error(s) logged by the game");
        foreach (string f in failures) Debug.Log("[TouchTest] failed: " + f);
        Debug.Log(failures.Count == 0 ? $"[TouchTest] PASS ({checks} checks)" : $"[TouchTest] FAIL ({failures.Count} of {checks} checks)");
        EditorApplication.Exit(failures.Count == 0 ? 0 : 1);
    }

    static void Check(string what, bool ok, string detail = "")
    {
        checks++;
        Debug.Log($"[TouchTest] {(ok ? "ok  " : "BAD ")} {what} {detail}");
        if (!ok) failures.Add(what + " " + detail);
    }

    static void Put(int id, Vector2 position)
    {
        fingers.RemoveAll(f => f.id == id);
        fingers.Add(new GameInput.Finger(id, position));
    }

    static void Lift(int id) => fingers.RemoveAll(f => f.id == id);

    static float Yaw(Component c) => c.transform.eulerAngles.y;
    static float Pitch(Camera cam) => Mathf.DeltaAngle(0f, cam.transform.localEulerAngles.x);

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    static void Teleport(Component who, Vector3 position)
    {
        var cc = who.GetComponent<CharacterController>();
        cc.enabled = false;
        who.transform.position = position;
        cc.enabled = true;
    }

    static IEnumerator<object> Script()
    {
        Time.captureFramerate = 30;
        GameInput.FingerSource = () => fingers;
        for (int i = 0; i < 20; i++) yield return null;     // the HUD lays itself out and publishes the buttons

        var gm = GameManager.Instance;
        var player = gm.Player;
        var cam = player.GetComponentInChildren<Camera>();
        float W = Screen.width, H = Screen.height, R = GameInput.StickRadius;
        Debug.Log($"[TouchTest] screen {W}x{H}, stick radius {R:0} px, jump {GameInput.JumpZone.centre}/{GameInput.JumpZone.radius:0}, blast {GameInput.BlastZone.centre}/{GameInput.BlastZone.radius:0}");
        Check("touch mode is on", GameInput.Touch);
        Check("the HUD published both buttons", GameInput.JumpZone.radius > 0f && GameInput.BlastZone.radius > 0f);

        // the rivals stand still for this test
        var bots = new List<AICompetitor>();
        foreach (var c in gm.Competitors)
            if (c is AICompetitor ai) { ai.enabled = false; bots.Add(ai); }

        Vector2 stick = new Vector2(W * 0.18f, H * 0.3f);
        Vector2 look = new Vector2(W * 0.5f, H * 0.62f);

        // ---- 1. stick pushed past its rim: sprint straight ahead
        Vector3 from = player.transform.position, fwd = player.transform.forward;
        Put(1, stick);
        yield return null;
        Put(1, stick + new Vector2(0f, R * 1.6f));
        for (int i = 0; i < 12; i++) yield return null;
        Vector3 moved = player.transform.position - from;
        float ahead = Vector3.Dot(moved, fwd), aside = Vector3.Cross(fwd, Flat(moved)).magnitude;
        Check("stick at the rim sprints forward", ahead > 2.7f && aside < 0.2f, $"({ahead:0.00} m ahead, {aside:0.00} m sideways in 0.4 s)");
        Check("the stick follows the thumb", GameInput.StickHeld && Mathf.Abs((GameInput.StickPosition - GameInput.StickOrigin).magnitude - R) < 1f);

        // ---- 2. lifting the thumb stops
        Lift(1);
        for (int i = 0; i < 6; i++) yield return null;
        from = player.transform.position;
        for (int i = 0; i < 6; i++) yield return null;
        Check("lifting the thumb stops", Flat(player.transform.position - from).magnitude < 0.02f && !GameInput.StickHeld);

        // ---- 3. dragging on the right turns the view
        float yaw0 = Yaw(player);
        Put(2, look);
        yield return null;
        for (int i = 1; i <= 15; i++) { Put(2, look + new Vector2(H * 0.3f * i / 15f, 0f)); yield return null; }
        yield return null;
        float turned = Mathf.DeltaAngle(yaw0, Yaw(player));
        Check("dragging right turns right", Mathf.Abs(turned - 34.5f) < 1.5f, $"({turned:0.0}° for 0.3 screen heights)");
        look += new Vector2(H * 0.3f, 0f);

        // ---- 4. walk with the left thumb while the right one keeps turning
        Put(1, stick);
        yield return null;
        Put(1, stick + new Vector2(0f, R * 0.5f));
        for (int i = 0; i < 5; i++) yield return null;
        from = player.transform.position;
        yaw0 = Yaw(player);
        for (int i = 1; i <= 10; i++) { Put(2, look + new Vector2(H * 0.1f * i / 10f, 0f)); yield return null; }
        yield return null;
        float walked = Flat(player.transform.position - from).magnitude;
        turned = Mathf.DeltaAngle(yaw0, Yaw(player));
        Check("walking and turning at the same time", walked > 0.6f && walked < 1.6f && Mathf.Abs(turned - 11.5f) < 1f,
            $"({walked:0.00} m at half stick while turning {turned:0.0}°)");
        look += new Vector2(H * 0.1f, 0f);

        // ---- 5. dragging up looks up, and back down again
        float pitch0 = Pitch(cam);
        for (int i = 1; i <= 10; i++) { Put(2, look + new Vector2(0f, H * 0.2f * i / 10f)); yield return null; }
        yield return null;
        float up = Pitch(cam) - pitch0;
        for (int i = 9; i >= 0; i--) { Put(2, look + new Vector2(0f, H * 0.2f * i / 10f)); yield return null; }
        yield return null;
        Check("dragging up looks up", Mathf.Abs(up + 18.4f) < 1.5f && Mathf.Abs(Pitch(cam) - pitch0) < 0.5f, $"({up:0.0}°, back to {Pitch(cam) - pitch0:0.0}°)");
        Lift(2);
        Lift(1);
        for (int i = 0; i < 8; i++) yield return null;

        // ---- 6. the jump button; a finger that started on a button never turns the view
        float ground = player.transform.position.y;
        yaw0 = Yaw(player);
        Put(3, GameInput.JumpZone.centre);
        yield return null;
        float top = ground;
        for (int i = 1; i <= 20; i++)
        {
            Put(3, GameInput.JumpZone.centre - new Vector2(H * 0.3f * i / 20f, 0f));    // slides off the button
            yield return null;
            top = Mathf.Max(top, player.transform.position.y);
        }
        Lift(3);
        Check("SKOK jumps", top - ground > 0.5f, $"({top - ground:0.00} m)");
        Check("a finger from a button does not turn the view", Mathf.Abs(Mathf.DeltaAngle(yaw0, Yaw(player))) < 0.01f);
        for (int i = 0; i < 15; i++) yield return null;

        // ---- 7. the blast button, with a rival under the crosshair
        var rival = bots[0];
        player.Collect(4);
        Teleport(rival, player.transform.position + Flat(cam.transform.forward).normalized * 6f);
        for (int i = 0; i < 4; i++) yield return null;
        Check("a rival under the crosshair arms the blast", player.AimTarget == rival && player.CanBlast);
        int carried = player.Backpack;      // (the farts lie at random: one may have been walked through on the way)
        Put(4, GameInput.BlastZone.centre);
        yield return null;
        yield return null;
        Lift(4);
        yield return null;
        Check("PRD! blasts the rival", rival.Stunned && player.Backpack == carried - player.blastCost, $"(backpack {carried} -> {player.Backpack})");

        // ---- 8. in the goal the farts go by themselves
        carried = player.Backpack;
        int scored = player.Score;
        Teleport(player, Arena.GoalPos + new Vector3(-1.5f, 0.95f, 1.5f));
        for (int i = 0; i < 60; i++) yield return null;
        Check("standing in the goal expels without a touch", player.InGoal && player.Backpack == 0 && player.Score == scored + carried && carried > 0,
            $"(score {scored} -> {player.Score}, backpack {carried} -> {player.Backpack})");

        // ---- 9. win, then tap for a new game (a tap straight away is ignored)
        for (int i = 0; i < 900 && !gm.GameOver; i++)
        {
            if (player.Backpack == 0) player.Collect(player.Capacity);
            yield return null;
        }
        Check("the player can win", gm.GameOver && gm.Winner == player);
        Put(5, new Vector2(W * 0.5f, H * 0.5f));
        yield return null;
        yield return null;
        Lift(5);
        for (int i = 0; i < 5; i++) yield return null;
        Check("a tap right after the win does not restart", GameManager.Instance == gm && gm.GameOver);

        double wait = EditorApplication.timeSinceStartup;
        for (int i = 0; i < 75 || EditorApplication.timeSinceStartup - wait < 2.5; i++) yield return null;
        Put(6, new Vector2(W * 0.5f, H * 0.5f));
        yield return null;
        yield return null;
        Lift(6);
        for (int i = 0; i < 15; i++) yield return null;
        var fresh = GameManager.Instance;
        Check("a tap after the banner starts a new game",
            fresh != null && fresh != gm && !fresh.GameOver && fresh.Player != null && fresh.Player.Score == 0 && Hud.Instance != null);
    }
}

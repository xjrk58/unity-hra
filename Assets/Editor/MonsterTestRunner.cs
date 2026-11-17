using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Dev tool: checks the fart monster without anybody playing.
///
///   Unity -batchmode -projectPath . -executeMethod MonsterTestRunner.Run
///
/// Runs a match at fixed 1/30 s steps with the bots standing still and checks
/// that: nothing happens for the first half minute; the lights start to fail
/// and the monster comes up out of the floor at about 45 s, on its prey's deck
/// and well away from it; it becomes dark; it reaches a player who stands
/// still, empties the backpack, scares and then stuns them, and leaves; it
/// follows its prey to another deck; a fart blast drives it off; it is gone
/// when the match ends; and a new game starts in daylight with the clock at
/// zero. Logs "[MonsterTest] PASS" or "[MonsterTest] FAIL". Do NOT pass -quit.
/// </summary>
[InitializeOnLoad]
public static class MonsterTestRunner
{
    const string ActiveKey = "MonsterTestRunner.Active";

    static readonly List<string> failures = new List<string>();
    static IEnumerator<object> script;
    static int lastFrame = -1, checks, errors;

    static MonsterTestRunner()
    {
        if (!SessionState.GetBool(ActiveKey, false)) return;
        FartAnimal.Enabled = false;         // (a fart dropped at the player's feet would spoil the sums)
        Application.logMessageReceived += (msg, stack, type) =>
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) errors++;
        };
        EditorApplication.update += Tick;
    }

    public static void Run()
    {
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
        foreach (string f in failures) Debug.Log("[MonsterTest] failed: " + f);
        Debug.Log(failures.Count == 0 ? $"[MonsterTest] PASS ({checks} checks)" : $"[MonsterTest] FAIL ({failures.Count} of {checks} checks)");
        EditorApplication.Exit(failures.Count == 0 ? 0 : 1);
    }

    static void Check(string what, bool ok, string detail = "")
    {
        checks++;
        Debug.Log($"[MonsterTest] {(ok ? "ok  " : "BAD ")} {what} {detail}");
        if (!ok) failures.Add(what + " " + detail);
    }

    static void Teleport(Component who, Vector3 position)
    {
        var cc = who.GetComponent<CharacterController>();
        cc.enabled = false;
        who.transform.position = position;
        cc.enabled = true;
    }

    static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

    static IEnumerator<object> Script()
    {
        Time.captureFramerate = 30;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        yield return null;
        yield return null;

        var gm = GameManager.Instance;
        var player = gm.Player;
        var monster = FartMonster.Instance;
        var rig = LightingRig.Instance;
        Check("there is a monster", monster != null);
        if (monster == null) yield break;
        foreach (var c in gm.Competitors)
            if (c is AICompetitor ai) ai.enabled = false;        // the rivals stand still

        // ---- 1. the first half minute is an ordinary sunny match
        while (Time.timeSinceLevelLoad < 30f) yield return null;
        Check("nothing for the first 30 s", monster.State == FartMonster.Phase.Dormant && !monster.Visible && rig.Dread <= 0f,
            $"({monster.State}, dread {rig.Dread:0.00})");

        // ---- 2. the lights begin to fail a few seconds before it comes
        while (Time.timeSinceLevelLoad < FartMonster.FirstAppearance - 3f) yield return null;
        Check("the lights fail before it appears", monster.State == FartMonster.Phase.Omen && rig.Dread > 0.1f && !monster.Visible,
            $"({monster.State}, dread {rig.Dread:0.00})");

        // ---- 3. it comes up out of the floor at about 45 s, on its prey's deck, a good way off
        while (!monster.Visible && Time.timeSinceLevelLoad < FartMonster.FirstAppearance + 5f) yield return null;
        float appeared = Time.timeSinceLevelLoad;
        var prey = monster.Target;
        float gap = prey != null ? Flat(monster.transform.position, prey.transform.position) : -1f;
        float dy = prey != null ? Mathf.Abs(monster.transform.position.y - (prey.transform.position.y - 0.9f)) : 99f;
        Check("it appears at about 45 s", monster.Visible && Mathf.Abs(appeared - FartMonster.FirstAppearance) < 0.5f, $"(at {appeared:0.0} s)");
        Check("it comes up on its prey's deck, well away from it", prey != null && dy < 1f && gap > 8f && gap < 15f,
            $"({(prey != null ? prey.DisplayName : "nobody")}, {gap:0.0} m away, {dy:0.0} m above)");
        Check("it does not come up in a hole or under a ramp",
            !Arena.InHole(Arena.FloorOfFeet(monster.transform.position.y), monster.transform.position.x, monster.transform.position.z, 1f) &&
            !Arena.OnRampFootprint(Arena.FloorOfFeet(monster.transform.position.y), monster.transform.position.x, monster.transform.position.z, 0.5f));

        for (int i = 0; i < 90; i++) yield return null;
        Check("it gets dark", rig.Dread > 0.95f && RenderSettings.fogDensity > 0.03f && rig.Sun.intensity < 0.3f,
            $"(dread {rig.Dread:0.00}, sun {rig.Sun.intensity:0.00})");
        Check("after rising it hunts", monster.State == FartMonster.Phase.Hunting, $"({monster.State})");

        // ---- 4. a player who stands still is caught: backpack emptied, scared, then stunned; then it leaves
        Teleport(player, new Vector3(-8f, 0.95f, -2.2f));
        yield return null;
        player.Collect(5);
        monster.Summon(new Vector3(-1f, 0f, -2.2f), player, FartMonster.Phase.Hunting);
        Vector3 from = monster.transform.position;
        for (int i = 0; i < 20; i++) yield return null;
        float covered = Flat(from, monster.transform.position);
        Check("it closes in on its prey", covered > 2.5f && covered < 5f && Flat(monster.transform.position, player.transform.position) < 5f,
            $"({covered:0.0} m in 0.67 s)");
        for (int i = 0; i < 120 && monster.State == FartMonster.Phase.Hunting; i++) yield return null;
        Check("it catches a player who stands still", monster.State == FartMonster.Phase.Feeding && monster.Victim == player, $"({monster.State})");
        Check("the catch empties the backpack and seizes the view", player.Backpack == 0 && player.Scared && !player.Stunned);
        for (int i = 0; i < 50; i++) yield return null;
        Check("after the scare the player is knocked out", player.Stunned && player.StunCause == "monster" && !player.Scared);
        for (int i = 0; i < 60 && monster.Visible; i++) yield return null;
        Check("then it melts away", monster.State == FartMonster.Phase.Lurking && !monster.Visible, $"({monster.State})");
        for (int i = 0; i < 170 && player.Stunned; i++) yield return null;
        Check("the player recovers", !player.Stunned);
        Check("between visits it is dusk, not day", rig.Dread > 0.3f && rig.Dread < 0.8f, $"(dread {rig.Dread:0.00})");

        // ---- 5. prey that leaves the deck is followed
        monster.Summon(new Vector3(-1f, 0f, -2.2f), player, FartMonster.Phase.Hunting);
        Teleport(player, new Vector3(-8f, Arena.FloorY(2) + 0.95f, -2.2f));
        bool followed = false;
        for (int i = 0; i < 240 && !followed; i++)
        {
            yield return null;
            followed = monster.Visible && monster.State != FartMonster.Phase.Dissolving &&
                       Mathf.Abs(monster.transform.position.y - Arena.FloorY(2)) < 1.2f;
        }
        Check("it follows its prey to another deck", followed && monster.Target == player,
            $"(monster at y {monster.transform.position.y:0.0}, {monster.State})");

        // ---- 6. a fart blast drives it off
        while (monster.State == FartMonster.Phase.Emerging) yield return null;
        player.Collect(4);
        Teleport(player, new Vector3(-8f, Arena.FloorY(2) + 0.95f, -2.2f));
        player.transform.rotation = Quaternion.LookRotation(Vector3.right);
        monster.Summon(new Vector3(0f, Arena.FloorY(2), -2.2f), player, FartMonster.Phase.Hunting);
        yield return null;
        yield return null;
        Check("the monster under the crosshair arms the blast", player.AimMonster && player.CanBlast && player.AimTarget == null);
        int before = player.Backpack;
        bool fired = player.BlastMonster(monster, player.blastCost);
        yield return null;
        Check("a blast drives it off", fired && monster.State == FartMonster.Phase.Dissolving && player.Backpack == before - player.blastCost,
            $"({monster.State}, backpack {player.Backpack})");
        for (int i = 0; i < 60; i++) yield return null;
        Check("and it stays away for a while", monster.State == FartMonster.Phase.Lurking && !monster.CanBeBlasted && !player.AimMonster);

        // ---- 7. it is gone when the match ends
        monster.Summon(new Vector3(0f, Arena.FloorY(2), -2.2f), player, FartMonster.Phase.Hunting);
        yield return null;
        for (int i = 0; i < 4 && !gm.GameOver; i++)
        {
            player.Collect(player.Capacity);
            while (!gm.GameOver && player.Expel()) { }
        }
        for (int i = 0; i < 300; i++) yield return null;
        Check("it is gone when the match ends", gm.GameOver && !monster.Visible && monster.State == FartMonster.Phase.Dormant && rig.Dread <= 0.01f,
            $"({monster.State}, dread {rig.Dread:0.00})");

        // ---- 8. a new game starts in daylight and the clock starts again
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        yield return null;
        yield return null;
        yield return null;
        var fresh = FartMonster.Instance;
        Check("a new game has a new, sleeping monster", fresh != null && fresh != monster && fresh.State == FartMonster.Phase.Dormant &&
                                                         LightingRig.Instance != null && LightingRig.Instance.Dread <= 0f);
        foreach (var c in GameManager.Instance.Competitors)
            if (c is AICompetitor ai) ai.enabled = false;
        while (Time.timeSinceLevelLoad < 30f) yield return null;
        Check("and it waits its 45 s again", fresh != null && fresh.State == FartMonster.Phase.Dormant && fresh.Appearances == 0);
    }
}

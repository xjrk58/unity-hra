using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Dev tool: checks the farting horses and unicorns without anybody playing.
///
///   Unity -batchmode -projectPath . -executeMethod AnimalTestRunner.Run
///
/// Runs a match at fixed 1/30 s steps with the player and the bots standing
/// still and the monster switched off, and checks that: there are three horses
/// and two unicorns, each with a fart of its own that is not there yet; in two
/// minutes of wandering every animal walks a good way, farts several times,
/// stays on its deck and never walks into a hole, a column or the low end of a
/// ramp; everything they drop lies on free floor of their own deck; a horse's
/// fart is worth one and a unicorn's rainbow fart three (never more than the
/// backpack holds); a collected one does not come back by itself, only with
/// the animal's next fart; nothing is dropped where there is no floor; an
/// animal gets out of the way of somebody who comes close, even out of a
/// corner; nobody starts the match next to one; and a new game has a new herd. Logs "[AnimalTest] PASS" or "[AnimalTest] FAIL".
/// Do NOT pass -quit.
/// </summary>
[InitializeOnLoad]
public static class AnimalTestRunner
{
    const string ActiveKey = "AnimalTestRunner.Active";
    const float WanderSeconds = 120f;

    class Track
    {
        public FartAnimal a;
        public Vector3 last;
        public float walked, nearestColumn = 99f, deepest;
        public int farts, lastSeq, badDrops, offDeck;
        public bool inHole;
    }

    static readonly List<string> failures = new List<string>();
    static IEnumerator<object> script;
    static int lastFrame = -1, checks, errors;

    static AnimalTestRunner()
    {
        if (!SessionState.GetBool(ActiveKey, false)) return;
        FartMonster.Enabled = false;        // this test is about the animals
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
        foreach (string f in failures) Debug.Log("[AnimalTest] failed: " + f);
        Debug.Log(failures.Count == 0 ? $"[AnimalTest] PASS ({checks} checks)" : $"[AnimalTest] FAIL ({failures.Count} of {checks} checks)");
        EditorApplication.Exit(failures.Count == 0 ? 0 : 1);
    }

    static void Check(string what, bool ok, string detail = "")
    {
        checks++;
        Debug.Log($"[AnimalTest] {(ok ? "ok  " : "BAD ")} {what} {detail}");
        if (!ok) failures.Add(what + " " + detail);
    }

    static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

    static void Teleport(Component who, Vector3 position)
    {
        var cc = who.GetComponent<CharacterController>();
        cc.enabled = false;
        who.transform.position = position;
        cc.enabled = true;
    }

    // How far inside one of the deck's no-go rectangles a point is (0 = outside them all).
    static float Depth(int deck, Vector3 p)
    {
        float deepest = 0f;
        foreach (Rect r in NavPlanner.Obstacles(deck))
        {
            float d = Mathf.Min(Mathf.Min(p.x - r.xMin, r.xMax - p.x), Mathf.Min(p.z - r.yMin, r.yMax - p.z));
            if (d > deepest) deepest = d;
        }
        return deepest;
    }

    static float ColumnGap(int deck, Vector3 p)
    {
        float nearest = 99f;
        if (deck >= Arena.FloorCount - 1) return nearest;       // the roof has no columns
        foreach (float gx in Arena.Grid)
            foreach (float gz in Arena.Grid)
            {
                if (Arena.InHole(deck, gx, gz, 0.6f) || Arena.InHole(deck + 1, gx, gz, 0.4f) || Arena.OnRampFootprint(deck, gx, gz, 0.6f)) continue;
                nearest = Mathf.Min(nearest, Vector2.Distance(new Vector2(p.x, p.z), new Vector2(gx, gz)));
            }
        return nearest;
    }

    static bool FreeFloor(int deck, Vector3 p) =>
        Mathf.Abs(p.x) <= Arena.Half - 1.5f && Mathf.Abs(p.z) <= Arena.Half - 1.5f &&
        !Arena.InHole(deck, p.x, p.z, 0.7f) && !Arena.OnRampFootprint(deck, p.x, p.z, 0.4f) && ColumnGap(deck, p) > 0.85f &&
        Mathf.Abs(p.y - (Arena.FloorY(deck) + 0.75f)) < 0.01f;

    static IEnumerator<object> Script()
    {
        Time.captureFramerate = 30;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        yield return null;
        yield return null;
        foreach (var cam in Camera.allCameras) cam.enabled = false;     // nothing to look at

        var gm = GameManager.Instance;
        var player = gm.Player;
        foreach (var c in gm.Competitors)
            if (c is AICompetitor ai) ai.enabled = false;        // the rivals stand still

        // ---- 1. the herd
        int horses = 0, unicorns = 0, ownFarts = 0, there = 0;
        foreach (var a in FartAnimal.All)
        {
            if (a.unicorn) unicorns++; else horses++;
            if (a.pickup != null && a.pickup.IsAnimalDrop && gm.Pickups.Contains(a.pickup)) ownFarts++;
            if (a.pickup != null && a.pickup.Available) there++;
        }
        int scattered = 0;
        foreach (var p in gm.Pickups) if (!p.IsAnimalDrop) scattered++;
        Check("three horses and two unicorns", horses == 3 && unicorns == 2, $"({horses} + {unicorns})");
        Check("each has a fart of its own, after the scattered ones", ownFarts == 5 && gm.Pickups.Count == scattered + 5 && gm.Pickups[scattered].IsAnimalDrop,
            $"({scattered} scattered + {ownFarts})");
        Check("which is not there before the animal has farted", there == 0);
        float nearestStart = 99f;
        foreach (var a in FartAnimal.All)
            foreach (var c in gm.Competitors)
                if (Mathf.Abs(c.transform.position.y - 0.95f - a.transform.position.y) < 2.5f)
                    nearestStart = Mathf.Min(nearestStart, Flat(a.transform.position, c.transform.position));
        Check("nobody starts the match next to an animal", nearestStart > 6f, $"(nearest {nearestStart:0.0} m)");
        if (FartAnimal.All.Count == 0) yield break;

        // ---- 2. two minutes of wandering
        var tracks = new List<Track>();
        foreach (var a in FartAnimal.All) tracks.Add(new Track { a = a, last = a.transform.position, lastSeq = a.FartSeq });
        float until = Time.time + WanderSeconds;
        while (Time.time < until)
        {
            yield return null;
            foreach (var k in tracks)
            {
                Vector3 p = k.a.transform.position;
                k.walked += Vector3.Distance(p, k.last);
                k.last = p;
                if (Mathf.Abs(p.y - Arena.FloorY(k.a.deck)) > 0.01f || Mathf.Abs(p.x) > Arena.Half - 1.8f || Mathf.Abs(p.z) > Arena.Half - 1.8f) k.offDeck++;
                if (Arena.InHole(k.a.deck, p.x, p.z, 0.2f)) k.inHole = true;
                k.nearestColumn = Mathf.Min(k.nearestColumn, ColumnGap(k.a.deck, p));
                k.deepest = Mathf.Max(k.deepest, Depth(k.a.deck, p));
                if (k.a.FartSeq != k.lastSeq)
                {
                    k.lastSeq = k.a.FartSeq;
                    k.farts++;
                    // (a fart with no free floor behind it leaves the old one where it was - also on free floor)
                    if (k.a.pickup.Available && !FreeFloor(k.a.deck, k.a.pickup.Home)) k.badDrops++;
                }
            }
        }
        bool walked = true, farted = true, onDeck = true, noHole = true, noColumn = true, noRamp = true, dropsOk = true;
        foreach (var k in tracks)
        {
            Debug.Log($"[AnimalTest] {(k.a.unicorn ? "unicorn" : "horse")} on deck {k.a.deck}: walked {k.walked:0} m, {k.farts} farts, " +
                      $"nearest column {k.nearestColumn:0.00} m, deepest into a no-go area {k.deepest:0.00} m, off deck {k.offDeck} frames, bad drops {k.badDrops}");
            if (k.walked < 60f) walked = false;
            if (k.farts < 4) farted = false;
            if (k.offDeck > 0) onDeck = false;
            if (k.inHole) noHole = false;
            if (k.nearestColumn < 0.7f) noColumn = false;       // (a column is 0.3 m thick, the animal about as much to each side)
            if (k.deepest > 0.2f) noRamp = false;
            if (k.badDrops > 0) dropsOk = false;
        }
        Check("every animal walks a good way in two minutes", walked);
        Check("and farts several times", farted);
        Check("they stay on their deck, away from the edge", onDeck);
        Check("they never walk into a hole", noHole);
        Check("or through a column", noColumn);
        Check("or into the railings and ramp ends the bots avoid", noRamp);
        Check("everything they drop lies on free floor of their deck", dropsOk);

        // ---- 3. what a fart is worth (only the animals' farts are left lying about for this)
        foreach (var p in gm.Pickups)
            if (!p.IsAnimalDrop) p.gameObject.SetActive(false);
        FartAnimal horse = null, unicorn = null;
        foreach (var a in FartAnimal.All)
        {
            if (!a.unicorn && a.deck == 0) horse = a;
            if (a.unicorn && a.deck == 1) unicorn = a;
        }
        Check("a horse on the ground deck and a unicorn on the first", horse != null && unicorn != null);
        if (horse == null || unicorn == null) yield break;
        foreach (var a in FartAnimal.All) a.Place(a.transform.position, a.transform.eulerAngles.y);      // everybody stands
        for (int i = 0; i < 130; i++) yield return null;        // (and the last rainbows of the wandering fade)

        Teleport(player, new Vector3(9f, 0.95f, 9f));
        player.LoseBackpack();
        horse.Place(new Vector3(-9f, 0f, 1.5f), 0f);
        int seq = horse.FartSeq;
        horse.FartNow();
        yield return null;
        Vector3 home = horse.pickup.Home;
        Check("a horse's fart hangs just behind it", horse.FartSeq == seq + 1 && horse.pickup.Available && horse.pickup.worth == 1 &&
                                                     Vector3.Distance(home, new Vector3(-9f, 0.75f, -0.1f)) < 0.05f, $"(at {home:0.00})");
        Teleport(player, new Vector3(home.x, 0.95f, home.z));
        for (int i = 0; i < 10; i++) yield return null;
        Check("and is worth one", player.Backpack == 1 && !horse.pickup.Available, $"(backpack {player.Backpack})");

        float y1 = Arena.FloorY(1);
        Teleport(player, new Vector3(9f, y1 + 0.95f, -9f));
        player.LoseBackpack();
        unicorn.Place(new Vector3(-9f, 0f, -3f), 180f);
        unicorn.FartNow();
        yield return null;
        home = unicorn.pickup.Home;
        Check("a unicorn's rainbow comes down three metres behind it and leaves a rainbow fart there",
            unicorn.pickup.Available && unicorn.pickup.worth == 3 && Vector3.Distance(home, new Vector3(-9f, y1 + 0.75f, 1.15f)) < 0.05f &&
            UnityEngine.Object.FindObjectsByType<RainbowArc>().Length == 1, $"(at {home:0.00})");
        Teleport(player, new Vector3(home.x, y1 + 0.95f, home.z));
        for (int i = 0; i < 10; i++) yield return null;
        Check("which is worth three", player.Backpack == 3 && !unicorn.pickup.Available, $"(backpack {player.Backpack})");

        // ---- 4. it does not come back by itself, only with the next fart
        Teleport(player, new Vector3(9f, y1 + 0.95f, -9f));
        int frames = Mathf.CeilToInt((unicorn.pickup.respawnDelay + 2f) * 30f);
        for (int i = 0; i < frames; i++) yield return null;
        Check("a collected one does not come back by itself", !unicorn.pickup.Available);
        Check("and the rainbow has faded by then", UnityEngine.Object.FindObjectsByType<RainbowArc>().Length == 0);
        unicorn.FartNow();
        yield return null;
        Check("it comes back with the animal's next fart", unicorn.pickup.Available);

        // ---- 5. never more than the backpack holds
        player.LoseBackpack();
        player.Collect(player.Capacity - 1);
        Teleport(player, new Vector3(home.x, y1 + 0.95f, home.z));
        for (int i = 0; i < 10; i++) yield return null;
        Check("a rainbow fart fills the backpack but not more", player.Backpack == player.Capacity && !unicorn.pickup.Available, $"(backpack {player.Backpack} of {player.Capacity})");
        unicorn.FartNow();
        for (int i = 0; i < 10; i++) yield return null;
        Check("a full backpack leaves it lying", player.Backpack == player.Capacity && unicorn.pickup.Available);
        Teleport(player, new Vector3(9f, y1 + 0.95f, -9f));

        // ---- 6. nowhere to drop it: over the hole, and with its tail at the edge
        unicorn.Place(new Vector3(-7.5f, 0f, 6f), 270f);     // the hole in this deck is right behind it
        Vector3 before = unicorn.pickup.Home;
        seq = unicorn.FartSeq;
        unicorn.FartNow();
        yield return null;
        Check("a rainbow over the hole leaves nothing new", unicorn.FartSeq == seq + 1 && unicorn.pickup.Home == before, $"(still at {unicorn.pickup.Home:0.00})");
        unicorn.Place(new Vector3(-11f, 0f, -3f), 90f);      // tail towards the edge
        unicorn.FartNow();
        yield return null;
        home = unicorn.pickup.Home;
        Check("with its tail at the edge the fart stays on the deck", unicorn.pickup.Available && FreeFloor(1, home), $"(at {home:0.00})");

        // ---- 7. they are shy: nobody ends up standing inside a horse
        Teleport(player, new Vector3(9f, 0.95f, 9f));
        horse.Place(new Vector3(-9f, 0f, 1.5f), 0f);
        horse.Release();
        for (int i = 0; i < 3; i++) yield return null;
        Teleport(player, new Vector3(-9f, 0.95f, 3.4f));        // right in front of its nose
        float closest = 99f;
        for (int i = 0; i < 45; i++)
        {
            yield return null;
            if (i >= 6) closest = Mathf.Min(closest, Flat(horse.transform.position, player.transform.position));
        }
        Check("an animal hurries out of the way of somebody who comes close", closest > 2.4f && Depth(0, horse.transform.position) < 0.2f,
            $"(kept {closest:0.0} m away, now at {horse.transform.position:0.0})");
        horse.Place(new Vector3(-10.9f, 0f, -10.9f), 45f);      // in a corner, the player between it and the open deck
        horse.Release();
        Teleport(player, new Vector3(-9.3f, 0.95f, -9.3f));
        for (int i = 0; i < 60; i++) yield return null;
        float corner = Flat(horse.transform.position, player.transform.position);
        Check("even out of a corner", corner > 2.4f, $"({corner:0.0} m away, at {horse.transform.position:0.0})");
        Teleport(player, new Vector3(9f, 0.95f, 9f));

        // ---- 8. a new game has a new herd
        var old = FartAnimal.All[0];
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        yield return null;
        yield return null;
        yield return null;
        int fresh = 0, total = 0;
        foreach (var a in FartAnimal.All)
            if (a != null && a != old && a.FartSeq == 0 && a.pickup != null && !a.pickup.Available) fresh++;
        foreach (var p in GameManager.Instance.Pickups) total++;
        Check("a new game has a new herd of five and the same number of farts", FartAnimal.All.Count == 5 && fresh == 5 && total == scattered + 5,
            $"({FartAnimal.All.Count} animals, {total} farts)");
    }
}

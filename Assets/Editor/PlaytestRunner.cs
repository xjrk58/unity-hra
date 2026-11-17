using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Dev tool: an unattended, accelerated playtest that checks the bots can
/// actually play — collect, change decks via the ramps, deliver and win —
/// without getting stuck.
///
///   Unity -batchmode -projectPath . -executeMethod PlaytestRunner.Run [-playtestSeconds 240] [-playtestMinFloor 1]
///
/// -playtestMinFloor N spawns pickups only on deck N and above, forcing the bots
/// onto the ramps. Results go to the log, lines prefixed with [Playtest].
/// </summary>
[InitializeOnLoad]
public static class PlaytestRunner
{
    const string ActiveKey = "PlaytestRunner.Active";
    const string SecondsKey = "PlaytestRunner.Seconds";
    const string FloorKey = "PlaytestRunner.MinFloor";

    class Track
    {
        public Competitor c;
        public Vector3 anchor;
        public float anchorTime;
        public float stuckSeconds, longestStuck;
        public int stuckEvents;
        public float distance;
        public Vector3 last;
        public readonly HashSet<int> floors = new HashSet<int>();
        public float firstScoreTime = -1f;
        public int falls;
    }

    static readonly List<Track> tracks = new List<Track>();
    static float nextReport;
    static bool started;
    static int phase;          // 0 = the match, 1 = a second scene load ("R" after a win)
    static int errors;
    static bool firstOk;

    static PlaytestRunner()
    {
        if (!SessionState.GetBool(ActiveKey, false)) return;
        GameBootstrap.PickupMinFloor = SessionState.GetInt(FloorKey, 0);
        Application.logMessageReceived += (msg, stack, type) =>
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) errors++;
        };
        EditorApplication.update += Tick;
    }

    public static void Run()
    {
        float seconds = 240f;
        int minFloor = 0;
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-playtestSeconds") seconds = float.Parse(args[i + 1]);
            if (args[i] == "-playtestMinFloor") minFloor = int.Parse(args[i + 1]);
        }
        SessionState.SetFloat(SecondsKey, seconds);
        SessionState.SetInt(FloorKey, minFloor);
        SessionState.SetBool(ActiveKey, true);
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        EditorApplication.update += Tick;
        EditorApplication.isPlaying = true;
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || GameManager.Instance == null) return;

        if (!started)
        {
            started = true;
            Time.captureFramerate = 30;            // fixed 1/30 s steps, as fast as the machine allows
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            foreach (var cam in Camera.allCameras) cam.enabled = false;   // nothing to look at
            foreach (var c in GameManager.Instance.Competitors)
                if (!c.IsPlayer) tracks.Add(new Track { c = c, anchor = c.transform.position, last = c.transform.position });
            Debug.Log($"[Playtest] start phase {phase}, bots={tracks.Count}, minFloor={GameBootstrap.PickupMinFloor}, recorded fart clips={FartAudio.RecordedClips}");
            return;
        }

        float t = Time.time;
        if (phase == 1)
        {
            // after the restart: everything must have been rebuilt and the bots must be playing again
            foreach (var k in tracks)
            {
                if (k.c == null) continue;
                Vector3 p1 = k.c.transform.position, d1 = p1 - k.last; d1.y = 0f;
                k.distance += d1.magnitude;
                k.last = p1;
            }
            if (Time.timeSinceLevelLoad < 20f) return;
            EditorApplication.update -= Tick;
            SessionState.SetBool(ActiveKey, false);
            bool moved = tracks.Count == 2;
            foreach (var k in tracks) if (k.c == null || k.distance < 30f) moved = false;
            bool hud = Hud.Instance != null && LightingRig.Instance != null && GameManager.Instance.Player != null;
            Debug.Log($"[Playtest] RESTART bots={tracks.Count} moved={moved} hud/lighting/player={hud} errorsLogged={errors}");
            bool ok = firstOk && moved && hud && errors == 0;
            Debug.Log(ok ? "[Playtest] PASS" : "[Playtest] FAIL");
            EditorApplication.Exit(ok ? 0 : 2);
            return;
        }

        foreach (var k in tracks)
        {
            Vector3 p = k.c.transform.position;
            Vector3 d = p - k.last; d.y = 0f;
            k.distance += d.magnitude;
            k.last = p;
            k.floors.Add(Arena.FloorOfFeet(p.y - 0.9f));
            if (k.firstScoreTime < 0f && k.c.Score > 0) k.firstScoreTime = t;
            if (p.y < -3f) k.falls++;

            // "stuck" = has not left a 0.6 m circle for a while, although free to act and not busy delivering
            bool idleOk = k.c.Stunned || k.c.InGoal;
            Vector3 fromAnchor = p - k.anchor; fromAnchor.y = 0f;
            if (fromAnchor.magnitude > 0.6f || idleOk)
            {
                float held = t - k.anchorTime;
                if (!idleOk && held > 3f)
                {
                    k.stuckEvents++;
                    Debug.Log($"[Playtest] {k.c.DisplayName} was stuck {held:0.0}s near {k.anchor:0.0} (floor {Arena.FloorOfFeet(k.anchor.y - 0.9f)})");
                }
                k.longestStuck = Mathf.Max(k.longestStuck, idleOk ? 0f : held);
                k.anchor = p;
                k.anchorTime = t;
            }
        }

        if (t >= nextReport)
        {
            nextReport = t + 15f;
            var sb = new StringBuilder($"[Playtest] t={t:0}s");
            foreach (var k in tracks)
                sb.Append($" | {k.c.DisplayName}: score {k.c.Score} pack {k.c.Backpack} floor {Arena.FloorOfFeet(k.c.transform.position.y - 0.9f)} pos {k.c.transform.position:0.0}{(k.c.Stunned ? " STUNNED" : "")}");
            Debug.Log(sb.ToString());
        }

        if (t >= SessionState.GetFloat(SecondsKey, 240f) || GameManager.Instance.GameOver)
            Finish(t);
    }

    static void Finish(float t)
    {
        var gm = GameManager.Instance;
        Debug.Log($"[Playtest] END t={t:0.0}s gameOver={gm.GameOver} winner={(gm.Winner != null ? gm.Winner.DisplayName : "-")}");
        bool ok = true;
        foreach (var k in tracks)
        {
            float held = t - k.anchorTime;
            if (!k.c.Stunned && !k.c.InGoal) k.longestStuck = Mathf.Max(k.longestStuck, held);
            var floors = new List<int>(k.floors); floors.Sort();
            Debug.Log($"[Playtest] RESULT {k.c.DisplayName}: score {k.c.Score}, walked {k.distance:0} m, decks visited [{string.Join(",", floors)}], " +
                      $"first delivery at {k.firstScoreTime:0}s, stuck events {k.stuckEvents}, longest stall {k.longestStuck:0.0}s, falls {k.falls}");
            // (a bot may legitimately not have delivered yet when a short match ends, but it must have been collecting)
            if (k.c.Score + k.c.Backpack == 0 || k.longestStuck > 8f || k.falls > 0) ok = false;
        }
        Debug.Log($"[Playtest] match {(ok ? "ok" : "NOT ok")}, errorsLogged={errors}; reloading the scene like pressing R");
        firstOk = ok;
        phase = 1;
        started = false;
        tracks.Clear();
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }
}

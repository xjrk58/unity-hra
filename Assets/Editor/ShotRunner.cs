using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Dev tool: renders screenshots of the running game without opening the Editor UI.
///
///   Unity -batchmode -projectPath . -executeMethod ShotRunner.Run -shotDir /some/dir
///
/// Enters Play mode, lets the game run for a moment, renders a handful of camera
/// angles to PNG files in -shotDir and quits. Do NOT pass -quit.
///
///   -mobileQuality   render with the quality level (and pipeline) phones use
///   -liteQuality     render with the lightest level, the one the build for old phones uses
///   -touchHud        show the touch-screen HUD in the 00_hud shots
/// </summary>
[InitializeOnLoad]
public static class ShotRunner
{
    const string ActiveKey = "ShotRunner.Active";
    const string DirKey = "ShotRunner.Dir";
    const int Width = 1600, Height = 900;
    const float WarmupSeconds = 2.5f;

    static double startTime = -1;
    static int frames;
    static int desktopQuality = -1;

    static ShotRunner()
    {
        if (SessionState.GetBool(ActiveKey, false))
        {
            ShaderUtil.allowAsyncCompilation = false;
            EditorApplication.update += Tick;
        }
    }

    public static void Run()
    {
        string dir = "Shots";
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-shotDir") dir = args[i + 1];

        ShaderUtil.allowAsyncCompilation = false;   // never capture the cyan "still compiling" placeholder
        SessionState.SetString(DirKey, dir);
        SessionState.SetBool(ActiveKey, true);
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        EditorApplication.update += Tick;
        EditorApplication.isPlaying = true;
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (startTime < 0)
        {
            startTime = EditorApplication.timeSinceStartup;
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-mobileQuality") >= 0) UseQuality(GraphicsSetup.MobileQualityLevel);
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-liteQuality") >= 0) UseQuality(GraphicsSetup.LiteQualityLevel);
        }
        frames++;
        if (frames < 30 || EditorApplication.timeSinceStartup - startTime < WarmupSeconds) return;

        EditorApplication.update -= Tick;
        SessionState.SetBool(ActiveKey, false);
        int code = 0;
        try { Capture(SessionState.GetString(DirKey, "Shots")); }
        catch (Exception e) { Debug.LogError("ShotRunner failed: " + e); code = 1; }
        if (desktopQuality >= 0) QualitySettings.SetQualityLevel(desktopQuality, true);
        EditorApplication.Exit(code);
    }

    // Switches to a quality level phones start in, before the reflection probes are captured.
    static void UseQuality(string level)
    {
        int index = Array.IndexOf(QualitySettings.names, level);
        if (index < 0) { Debug.LogError("ShotRunner: no quality level " + level); return; }
        desktopQuality = QualitySettings.GetQualityLevel();
        QualitySettings.SetQualityLevel(index, true);
        Debug.Log($"ShotRunner quality: {QualitySettings.names[index]}, pipeline {QualitySettings.renderPipeline}, " +
                  $"realtime probes {QualitySettings.realtimeReflectionProbes}");
    }

    static void Capture(string dir)
    {
        Directory.CreateDirectory(dir);
        var playerCam = Camera.main != null ? Camera.main : UnityEngine.Object.FindAnyObjectByType<Camera>();
        if (playerCam == null) throw new Exception("no camera in scene");

        Vector3 home = playerCam.transform.position;
        Quaternion homeRot = playerCam.transform.rotation;

        // 1) what the player sees on spawn
        Shot(playerCam, dir, "01_player", home, homeRot);

        // 1b) the same view with the HUD, at the aspect ratio of the (batch-mode) screen
        // so the canvas layout is exactly what a player with that screen would get
        var gm = GameManager.Instance;
        if (gm != null && gm.Player != null)
        {
            var bots = new System.Collections.Generic.List<Competitor>(gm.Competitors);
            gm.Player.Collect(3);
            // The HUD is a screen-space overlay, which only exists on a real screen. For this
            // check, hang the canvas in front of the camera instead (same layout; colours pass
            // through the scene's post-processing here, which they do not in the game).
            var canvas = Hud.Instance != null ? Hud.Instance.GetComponentInChildren<Canvas>() : null;
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = playerCam;
                canvas.planeDistance = 0.3f;
            }
            // a 4:3 monitor is the tightest fit for the desktop HUD; phones are wide
            int hudWidth = GameInput.Touch ? 2340 : 1440;
            Shot(playerCam, dir, "00_hud", home, homeRot, hudWidth, 1080);
            if (GameInput.Touch) Shot(playerCam, dir, "00_hud_tablet", home, homeRot, 1440, 1080);
            gm.Player.Stun(4f);
            Shot(playerCam, dir, "00_hud_stunned", home, homeRot, hudWidth, 1080);
            if (canvas != null) canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        // 2) whole arena from outside
        Shot(playerCam, dir, "02_overview", new Vector3(-27f, 13f, -30f), LookAt(new Vector3(-27f, 13f, -30f), new Vector3(0f, 7f, 0f)));

        // 3) a rival up close
        var rival = GameObject.Find("AI_Pšouk");
        if (rival != null)
        {
            Vector3 p = rival.transform.position + rival.transform.forward * 2.6f + rival.transform.right * 1.2f + Vector3.up * 0.5f;
            Shot(playerCam, dir, "03_rival", p, LookAt(p, rival.transform.position + Vector3.up * 0.1f));
        }

        var rival2 = GameObject.Find("AI_Bzdina");
        if (rival2 != null)
        {
            Transform t = rival2.transform;
            Vector3 p = t.position + t.forward * 1.9f + t.right * 0.5f + Vector3.up * 0.35f;
            Shot(playerCam, dir, "10_face", p, LookAt(p, t.position + Vector3.up * 0.25f));
            p = t.position - t.forward * 2.0f - t.right * 0.9f + Vector3.up * 0.4f;
            Shot(playerCam, dir, "11_back", p, LookAt(p, t.position + Vector3.up * 0.1f));
        }

        // 4) the goal
        var goal = GameObject.Find("Cil");
        if (goal != null)
        {
            Vector3 p = goal.transform.position + new Vector3(-8.5f, 1.9f, 2.5f);
            Shot(playerCam, dir, "04_goal", p, LookAt(p, goal.transform.position + Vector3.up * 1.2f));
        }

        // 5) upper floor, looking along the ramp
        Vector3 up = new Vector3(8f, Arena.FloorY(1) + 1.7f, -8f);
        Shot(playerCam, dir, "05_floor1", up, LookAt(up, new Vector3(-3f, Arena.FloorY(1) + 0.5f, 6f)));

        // 6) a pickup up close
        // (the first one that has not just been collected; then several from across a deck)
        FartPickup pickup = null;
        if (gm != null)
            foreach (var pk in gm.Pickups)
                if (pk != null && pk.Available) { pickup = pk; break; }
        if (pickup != null)
        {
            Vector3 p = pickup.transform.position + new Vector3(1.6f, 0.5f, -1.6f);
            Shot(playerCam, dir, "06_pickup", p, LookAt(p, pickup.transform.position));
        }
        Vector3 corner = new Vector3(-11f, 1.7f, -11f);
        Shot(playerCam, dir, "06_pickups_deck0", corner, LookAt(corner, new Vector3(6f, 0.6f, 8f)));
        corner = new Vector3(-11f, Arena.FloorY(3) + 1.7f, -11f);
        Shot(playerCam, dir, "06_pickups_roof", corner, LookAt(corner, new Vector3(6f, Arena.FloorY(3) + 0.6f, 8f)));

        // straight down onto the building
        Vector3 above = new Vector3(0f, 62f, 0f);
        Shot(playerCam, dir, "12_top", above, Quaternion.LookRotation(Vector3.down, Vector3.forward));

        // 7) open top deck
        Vector3 roof = new Vector3(-9f, Arena.FloorY(3) + 1.7f, -9f);
        Shot(playerCam, dir, "07_roof", roof, LookAt(roof, new Vector3(4f, Arena.FloorY(3) + 1.0f, 5f)));

        // 8) foot of the first ramp, looking up it
        Vector3 rampFoot = new Vector3(3.2f, 1.7f, 12.2f);
        Shot(playerCam, dir, "08_ramp", rampFoot, LookAt(rampFoot, new Vector3(-0.5f, 3.2f, 3f)));

        // 9) toward the sun across the ground deck
        Vector3 sunView = new Vector3(8f, 1.7f, -4f);
        Shot(playerCam, dir, "09_sunward", sunView, LookAt(sunView, new Vector3(-6f, 2.3f, 3.5f)));

        AnimalShots(playerCam, dir);
        MonsterShots(playerCam, dir);

        playerCam.transform.SetPositionAndRotation(home, homeRot);
        Debug.Log($"ShotRunner rendered with {UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.name}");
        Benchmark(playerCam, home, homeRot);

        // the end of a match: the winner and the final ranking
        if (gm != null && gm.Player != null)
        {
            for (int i = 0; i < 4 && !gm.GameOver; i++)
            {
                gm.Player.Collect(gm.Player.Capacity);
                while (!gm.GameOver && gm.Player.Expel()) { }
            }
            var canvas = Hud.Instance != null ? Hud.Instance.GetComponentInChildren<Canvas>() : null;
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = playerCam;
                canvas.planeDistance = 0.3f;
            }
            Shot(playerCam, dir, "13_ranking", home, homeRot, GameInput.Touch ? 2340 : 1440, 1080);
            if (canvas != null) canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }
    }

    // A horse; a unicorn in the middle of a rainbow fart, from the side and from behind; and
    // the rainbow fart it leaves. Both are stood in the open bay between two rows of columns.
    static void AnimalShots(Camera cam, string dir)
    {
        FartAnimal horse = null, unicorn = null;
        foreach (var a in FartAnimal.All)
        {
            if (a.unicorn && unicorn == null) unicorn = a;
            if (!a.unicorn && horse == null) horse = a;
        }
        if (horse != null)
        {
            horse.Place(new Vector3(-9f, 0f, 1.5f), 0f);
            var visual = horse.GetComponent<AnimalVisual>();
            if (visual != null) visual.Settle();
            Transform t = horse.transform;
            Vector3 p = t.position + t.forward * 2.8f + t.right * 3.2f + Vector3.up * 1.5f;
            Shot(cam, dir, "30_horse", p, LookAt(p, t.position + Vector3.up * 1.0f));
        }
        if (unicorn != null)
        {
            float y = Arena.FloorY(unicorn.deck);
            unicorn.Place(new Vector3(-9f, 0f, -3f), 180f);
            unicorn.FartNow();
            foreach (var arc in UnityEngine.Object.FindObjectsByType<RainbowArc>()) arc.Advance(0.9f);
            foreach (var ps in UnityEngine.Object.FindObjectsByType<ParticleSystem>())
                if (ps.name.StartsWith("Rainbow")) ps.Simulate(0.6f, true, true);
            var visual = unicorn.GetComponent<AnimalVisual>();
            if (visual != null) visual.Settle();

            Vector3 p = new Vector3(-3.3f, y + 1.7f, -1.5f);
            Shot(cam, dir, "31_unicorn_rainbow", p, LookAt(p, new Vector3(-9f, y + 1.45f, -1.5f)));

            // the way a player usually meets it: from behind and a little to one side
            p = new Vector3(-7.3f, y + 1.7f, 7.4f);
            Shot(cam, dir, "33_rainbow_behind", p, LookAt(p, new Vector3(-9f, y + 1.3f, -1.5f)));

            if (unicorn.pickup != null && unicorn.pickup.Available)
            {
                Vector3 at = unicorn.pickup.Home;
                p = at + new Vector3(2.6f, 0.55f, 1.7f);
                Shot(cam, dir, "32_rainbow_fart", p, LookAt(p, at + Vector3.up * 0.1f));
            }
        }
    }

    // The fart monster the way the player meets it: far down a deck, closing in, in the
    // face when it catches you, from the side, and with the HUD's warnings.
    static void MonsterShots(Camera cam, string dir)
    {
        var monster = FartMonster.Instance;
        var gm = GameManager.Instance;
        if (monster == null || gm == null || gm.Player == null) return;
        var pl = gm.Player;
        Vector3 plPos = pl.transform.position;
        Quaternion plRot = pl.transform.rotation;

        // a clear run across the ground deck, between the column rows
        pl.transform.SetPositionAndRotation(new Vector3(-11f, 0.95f, -2.2f), Quaternion.LookRotation(Vector3.right));
        Vector3 eye = pl.transform.position + Vector3.up * 0.7f, feet = pl.transform.position - Vector3.up * 0.9f;
        Quaternion ahead = pl.transform.rotation;
        Vector3 fwd = Vector3.right;

        monster.Summon(feet + fwd * 10f, pl, FartMonster.Phase.Hunting, 5f);
        Shot(cam, dir, "20_monster_far", eye, ahead);

        monster.Summon(feet + fwd * 4.2f, pl, FartMonster.Phase.Hunting, 5f);
        Shot(cam, dir, "21_monster_near", eye, LookAt(eye, monster.ChestPosition + Vector3.up * 0.4f));

        // (the game narrows the view by 14 degrees for the scare)
        monster.Summon(feet + fwd * FartMonster.CatchDistance, pl, FartMonster.Phase.Feeding, 0.6f);
        float fov = cam.fieldOfView;
        cam.fieldOfView = fov - 14f;
        Shot(cam, dir, "22_monster_catch", eye, LookAt(eye, monster.Visual.Face.position));
        cam.fieldOfView = fov;

        monster.Summon(feet + fwd * 6f, pl, FartMonster.Phase.Hunting, 5f);
        Vector3 side = feet + fwd * 3.2f + Vector3.forward * 5.2f + Vector3.up * 1.7f;
        Shot(cam, dir, "23_monster_side", side, LookAt(side, monster.ChestPosition));

        // its face, level and close (to check the jaws, not a view the player gets)
        monster.Summon(feet + fwd * 3f, pl, FartMonster.Phase.Hunting, 5f);
        Vector3 facePos = monster.Visual.Face.position;
        Vector3 level = facePos - fwd * 1.9f + Vector3.forward * 0.9f - Vector3.up * 0.25f;
        Shot(cam, dir, "25_monster_face", level, LookAt(level, facePos - Vector3.up * 0.15f));

        // the HUD while it hunts you
        monster.Summon(feet + fwd * 7f, pl, FartMonster.Phase.Hunting, 5f);
        var canvas = Hud.Instance != null ? Hud.Instance.GetComponentInChildren<Canvas>() : null;
        if (canvas != null)
        {
            Hud.Instance.Announce("PRDOVÁ PŘÍŠERA!", "Jde po tobě! Uteč – nebo ji zažeň prdem.", new Color(1f, 0.16f, 0.08f), 3.4f);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 0.3f;
            Shot(cam, dir, "24_monster_hud", eye, ahead, GameInput.Touch ? 2340 : 1440, 1080);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Hud.Instance.Announce("", "", Color.white, 0f);
        }

        var parts = new System.Text.StringBuilder("ShotRunner monster parts:");
        foreach (var t in monster.GetComponentsInChildren<Transform>(true))
            if (t.name.StartsWith("Monster") || t.name == "Model")
                parts.Append($" {t.name} rot={t.localRotation.eulerAngles:0} scale={t.localScale:0.00};");
        Debug.Log(parts.ToString());

        // back to the day
        monster.Summon(new Vector3(0f, -50f, 0f), null, FartMonster.Phase.Dormant);
        pl.transform.SetPositionAndRotation(plPos, plRot);
    }

    // Rough frame cost at 1080p from the spawn view (render + forced GPU sync), to sanity-check
    // that the quality settings are playable on this machine. Only indicative: it runs inside
    // the editor's render loop and swings a lot with whatever else the machine is doing.
    static void Benchmark(Camera cam, Vector3 pos, Quaternion rot)
    {
        cam.transform.SetPositionAndRotation(pos, rot);
        var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var probe = new Texture2D(1, 1, TextureFormat.RGB24, false);
        cam.targetTexture = rt;
        var sw = new System.Diagnostics.Stopwatch();
        const int warm = 10, n = 60;
        for (int i = 0; i < warm + n; i++)
        {
            if (i == warm) sw.Start();
            cam.Render();
            RenderTexture.active = rt;
            probe.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);   // forces the GPU to finish the frame
        }
        sw.Stop();
        RenderTexture.active = null;
        cam.targetTexture = null;
        double ms = sw.Elapsed.TotalMilliseconds / n;
        Debug.Log($"ShotRunner benchmark: {ms:0.0} ms per 1080p frame (~{1000.0 / ms:0} fps) on {SystemInfo.graphicsDeviceName} / {SystemInfo.graphicsDeviceType}");
        UnityEngine.Object.DestroyImmediate(probe);
        rt.Release();
    }

    static Quaternion LookAt(Vector3 from, Vector3 to) => Quaternion.LookRotation(to - from, Vector3.up);

    static void Shot(Camera cam, string dir, string name, Vector3 pos, Quaternion rot, int Width = ShotRunner.Width, int Height = ShotRunner.Height)
    {
        cam.transform.SetPositionAndRotation(pos, rot);

        var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        rt.antiAliasing = 4;
        var prevTarget = cam.targetTexture;
        var prevActive = RenderTexture.active;

        cam.targetTexture = rt;
        if (Hud.Instance != null) Hud.Instance.Refresh();
        Canvas.ForceUpdateCanvases();
        UnityEngine.Rendering.ScriptableRenderContext.EmitGeometryForCamera(cam);   // UI attached to this camera
        cam.Render();
        cam.Render(); // second pass lets temporal effects settle

        RenderTexture.active = rt;
        var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        tex.Apply();

        cam.targetTexture = prevTarget;
        RenderTexture.active = prevActive;

        string path = Path.Combine(dir, name + ".png");
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Debug.Log("ShotRunner wrote " + path);

        UnityEngine.Object.DestroyImmediate(tex);
        rt.Release();
        UnityEngine.Object.DestroyImmediate(rt);
    }
}

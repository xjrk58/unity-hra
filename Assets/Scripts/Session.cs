using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// What kind of game this run of the program is playing, remembered across
/// scene loads (a restart reloads the scene; the choice made in the menu must
/// survive that):
///
///   Menu    nothing chosen yet: the title screen
///   Solo    one player and two bots, everything on this device
///   Host    two players: this device runs the world and the other one joins it
///   Client  two players: this device joined a host and shows what the host says
///
/// Started without a window (the dev tools, the tests) the game skips the menu
/// and plays solo; "-host" and "-join ADDRESS" on the command line go straight
/// into a two-player game.
/// </summary>
public static class Session
{
    public enum Mode { Menu, Solo, Host, Client }

    public static Mode Current = Mode.Menu;

    public static bool IsHost => Current == Mode.Host;
    public static bool IsClient => Current == Mode.Client;
    public static bool IsNet => Current == Mode.Host || Current == Mode.Client;

    /// <summary>Joining device: where the host scattered the farts (same order as the host's list).</summary>
    public static readonly List<Vector3> Pickups = new List<Vector3>();

    /// <summary>Counts matches, so that messages from the previous one are recognised and dropped.</summary>
    public static byte MatchId;

    /// <summary>What to do from the command line instead of showing the menu ("" = nothing).</summary>
    public static string AutoJoin = "";
    public static bool AutoHost;

    /// <summary>Why the last two-player game ended early, for the menu to show ("" = it did not).</summary>
    public static string Notice = "";

    static bool resolved;

    /// <summary>Reads the command line once per run.</summary>
    public static void Resolve()
    {
        if (resolved) return;
        resolved = true;
        var args = Environment.GetCommandLineArgs();
        bool menu = false;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-host") AutoHost = true;
            else if (args[i] == "-join" && i + 1 < args.Length) AutoJoin = args[i + 1];
            else if (args[i] == "-menu") menu = true;
        }
        if (AutoHost || AutoJoin != "") return;                         // the menu's lobby takes it from there
        if (Application.isBatchMode && !menu) Current = Mode.Solo;      // dev tools and tests: straight into a match
    }

    public static bool HasFlag(string flag) => Array.IndexOf(Environment.GetCommandLineArgs(), flag) >= 0;

    public static float FlagValue(string flag, float fallback)
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == flag && float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v)) return v;
        return fallback;
    }

    /// <summary>Back to the title screen (the two-player game fell apart, or was left).</summary>
    public static void ToMenu(string notice)
    {
        Notice = notice;
        NetLink.Close();
        Current = Mode.Menu;
        AutoHost = false;
        AutoJoin = "";
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    public static void Reload()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    // ------------------------------------------------------------------ the "start" message

    // Only the scattered farts are listed: the one each horse and unicorn brings is made
    // with the animal on both devices, after these (see GameBootstrap.SpawnHerd).
    public static void WriteStart(BinaryWriter w, IReadOnlyList<FartPickup> pickups)
    {
        int scattered = 0;
        foreach (var p in pickups) if (!p.IsAnimalDrop) scattered++;
        w.Write(MatchId);
        w.Write((byte)scattered);
        foreach (var p in pickups)
        {
            if (p.IsAnimalDrop) continue;
            Vector3 at = p.Home;
            w.Write(at.x); w.Write(at.y); w.Write(at.z);
        }
    }

    public static void ReadStart(BinaryReader r)
    {
        MatchId = r.ReadByte();
        int n = r.ReadByte();
        Pickups.Clear();
        for (int i = 0; i < n; i++) Pickups.Add(new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()));
    }
}

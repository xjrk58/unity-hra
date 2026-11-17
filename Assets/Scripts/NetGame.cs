using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Keeps a two-player game on two devices in step. The host's device runs the
/// world - the bots, the farts lying about, the monster, the scores, who has
/// won - and tells the other device about it twenty times a second. Each device
/// moves its own player and reports where it is; anything the joining player
/// does that changes the game (expelling at the goal, blasting, asking for a
/// new game) is a request the host carries out.
///
/// Messages (see <see cref="NetLink"/>): Start (host: a match begins, here are
/// the farts), Ready (joiner: my world is built), State (host: everything),
/// Input (joiner: where I am), Action (joiner: do this for me).
/// </summary>
public class NetGame : MonoBehaviour
{
    public const byte MsgStart = 2, MsgReady = 3, MsgState = 5, MsgInput = 6, MsgAction = 7;
    public const byte ActExpel = 1, ActBlastRival = 2, ActBlastMonster = 3, ActRestart = 4;

    /// <summary>"The monster" where a message names who was blasted.</summary>
    public const int MonsterIndex = 100;

    const float SendInterval = 0.05f;
    const float WaitForJoiner = 20f;

    public static NetGame Instance { get; private set; }

    /// <summary>How many state messages this device has taken in (joiner) or sent (host).</summary>
    public int States { get; private set; }

    NetPuppet remote;           // host: the joining player
    bool waiting, dropped;
    float nextSend, waitUntil;

    /// <summary>Host: the match is built; tell the joiner and hold the start until it is ready.</summary>
    public static void BuildHost(NetPuppet remote)
    {
        var gm = GameManager.Instance;
        Instance = new GameObject("NetGame").AddComponent<NetGame>();
        Instance.remote = remote;
        Session.MatchId++;
        NetLink.Send(MsgStart, w => Session.WriteStart(w, gm.Pickups));
        Instance.waiting = true;
        Instance.waitUntil = Time.realtimeSinceStartup + WaitForJoiner;
        gm.Paused = true;
        Time.timeScale = 0f;
        Debug.Log($"[Net] host: match {Session.MatchId} built, waiting for the joiner");
    }

    /// <summary>Joiner: the world is built from the host's Start; say so.</summary>
    public static void BuildClient()
    {
        Instance = new GameObject("NetGame").AddComponent<NetGame>();
        GameManager.Instance.Paused = true;
        Time.timeScale = 0f;
        NetLink.Send(MsgReady, w => w.Write(Session.MatchId));
        Debug.Log($"[Net] joiner: match {Session.MatchId} built with {Session.Pickups.Count} farts");
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Joiner: ask the host to do something on this player's behalf.</summary>
    public static void SendAction(byte kind, int arg = 0)
    {
        if (!Session.IsClient) return;
        NetLink.Send(MsgAction, w => { w.Write(Session.MatchId); w.Write(kind); w.Write((byte)arg); });
    }

    /// <summary>Host: a new match for both.</summary>
    public static void HostRestart()
    {
        if (!Session.IsHost) return;
        if (!NetLink.Connected)
        {
            // the other player has gone: the next match is an ordinary solo one
            NetLink.Close();
            Session.Current = Session.Mode.Solo;
        }
        Session.Reload();       // (still two players: the new scene's BuildHost sends the Start)
    }

    void Update()
    {
        if (Session.IsHost) HostUpdate();
        else if (Session.IsClient) ClientUpdate();
    }

    // ------------------------------------------------------------------ host

    void Unpause()
    {
        if (!waiting) return;
        waiting = false;
        GameManager.Instance.Paused = false;
        Time.timeScale = 1f;
        Debug.Log("[Net] host: go");
    }

    void HostUpdate()
    {
        var gm = GameManager.Instance;
        while (NetLink.Receive(out byte type, out BinaryReader r))
        {
            if (type == MsgReady)
            {
                if (r.ReadByte() == Session.MatchId) Unpause();
            }
            else if (type == MsgInput)
            {
                if (r.ReadByte() != Session.MatchId) continue;
                Vector3 p = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                float yaw = r.ReadSingle();
                if (remote != null && !remote.Gone) remote.MoveTo(p, yaw);
            }
            else if (type == MsgAction)
            {
                if (r.ReadByte() != Session.MatchId) continue;
                byte kind = r.ReadByte();
                int arg = r.ReadByte();
                Act(kind, arg);
            }
        }

        if (waiting && Time.realtimeSinceStartup > waitUntil) Unpause();

        if (!NetLink.Connected)
        {
            if (!dropped)
            {
                dropped = true;             // the joiner hung up: play on alone
                Unpause();
                if (remote != null) remote.Leave();
                if (Hud.Instance != null) Hud.Instance.Announce("Druhý hráč se odpojil", "Hraješ dál.", new Color(1f, 0.85f, 0.2f), 4f);
                Debug.Log("[Net] host: the joiner left");
            }
            return;
        }

        if (Time.unscaledTime >= nextSend)
        {
            nextSend = Time.unscaledTime + SendInterval;
            NetLink.Send(MsgState, WriteState);
            States++;
        }
    }

    void Act(byte kind, int arg)
    {
        var gm = GameManager.Instance;
        if (remote == null || remote.Gone || gm == null) return;
        if (kind == ActRestart)
        {
            if (!gm.GameOver) return;
            Debug.Log("[Net] host: the joiner asked for a new match");
            HostRestart();
            return;
        }
        if (gm.GameOver || gm.Paused || remote.Stunned) return;
        int cost = gm.Player != null ? gm.Player.blastCost : 2;
        float stun = gm.Player != null ? gm.Player.stunDuration : 10f;
        switch (kind)
        {
            case ActExpel:
                if (remote.InGoal) remote.Expel();
                break;
            case ActBlastRival:
                if (arg < gm.Competitors.Count) remote.BlastRival(gm.Competitors[arg], cost, stun);
                break;
            case ActBlastMonster:
                bool hit = remote.BlastMonster(FartMonster.Instance, cost);
                Debug.Log("[Net] host: the joiner blasted the monster: " + hit);
                break;
        }
    }

    void WriteState(BinaryWriter w)
    {
        var gm = GameManager.Instance;
        w.Write(Session.MatchId);
        w.Write(gm.MatchTime);
        w.Write((byte)((gm.Paused ? 1 : 0) | (gm.GameOver ? 2 : 0)));
        w.Write((sbyte)(gm.Winner != null ? gm.IndexOf(gm.Winner) : -1));

        w.Write((byte)gm.Competitors.Count);
        foreach (var c in gm.Competitors)
        {
            Vector3 p = c.transform.position;
            w.Write(p.x); w.Write(p.y); w.Write(p.z);
            w.Write(c.transform.eulerAngles.y);
            w.Write((byte)c.Backpack);
            w.Write((byte)c.Score);
            w.Write((byte)((c.Stunned ? 1 : 0) | (c.InGoal ? 2 : 0) | (c.StunCause == "monster" ? 4 : 0) | (c.Gone ? 8 : 0)));
            w.Write(c.StunRemaining);
            w.Write((byte)c.BlastSeq);
            w.Write((byte)c.BlastTarget);
        }

        uint mask = 0;
        for (int i = 0; i < gm.Pickups.Count && i < 32; i++)
            if (gm.Pickups[i] != null && gm.Pickups[i].Available) mask |= 1u << i;
        w.Write(mask);

        var m = FartMonster.Instance;
        w.Write(m != null);
        if (m != null) m.NetWrite(w);

        w.Write((byte)FartAnimal.All.Count);
        foreach (var a in FartAnimal.All) a.NetWrite(w);
    }

    // ------------------------------------------------------------------ joiner

    void ClientUpdate()
    {
        var gm = GameManager.Instance;
        while (NetLink.Receive(out byte type, out BinaryReader r))
        {
            if (type == MsgStart)
            {
                Session.ReadStart(r);       // the host started a new match: build it
                Session.Current = Session.Mode.Client;
                Debug.Log("[Net] joiner: new match " + Session.MatchId);
                Session.Reload();
                return;
            }
            if (type == MsgState) ReadState(r);
        }

        if (!NetLink.Connected)
        {
            Debug.Log("[Net] joiner: the host is gone");
            Session.ToMenu("Spojení s hostitelem se přerušilo.");
            return;
        }

        if (Time.unscaledTime >= nextSend && gm.Player != null)
        {
            nextSend = Time.unscaledTime + SendInterval;
            Vector3 p = gm.Player.transform.position;
            float yaw = gm.Player.transform.eulerAngles.y;
            NetLink.Send(MsgInput, w =>
            {
                w.Write(Session.MatchId);
                w.Write(p.x); w.Write(p.y); w.Write(p.z);
                w.Write(yaw);
            });
        }
    }

    void ReadState(BinaryReader r)
    {
        var gm = GameManager.Instance;
        if (r.ReadByte() != Session.MatchId) return;        // left over from the previous match
        States++;
        gm.MatchTime = r.ReadSingle();
        int flags = r.ReadByte();
        int winner = r.ReadSByte();
        bool paused = (flags & 1) != 0;
        if (gm.Paused != paused)
        {
            gm.Paused = paused;
            Time.timeScale = paused ? 0f : 1f;
        }

        int n = r.ReadByte();
        for (int i = 0; i < n; i++)
        {
            Vector3 p = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            float yaw = r.ReadSingle();
            int backpack = r.ReadByte(), score = r.ReadByte(), f = r.ReadByte();
            float stunLeft = r.ReadSingle();
            int blastSeq = r.ReadByte(), blastTarget = r.ReadByte();
            if (i >= gm.Competitors.Count) continue;
            var c = gm.Competitors[i];
            if (c is NetPuppet puppet)
            {
                if ((f & 8) != 0) { if (!puppet.Gone) puppet.Leave(); }
                else puppet.MoveTo(p, yaw);
            }
            c.NetApply(backpack, score, (f & 1) != 0, stunLeft, (f & 4) != 0 ? "monster" : null, (f & 2) != 0, blastSeq, blastTarget);
        }

        uint mask = r.ReadUInt32();
        for (int i = 0; i < gm.Pickups.Count && i < 32; i++)
            if (gm.Pickups[i] != null) gm.Pickups[i].NetSetAvailable((mask & (1u << i)) != 0);

        if (r.ReadBoolean())
        {
            if (FartMonster.Instance != null) FartMonster.Instance.NetRead(r);
            else return;        // (cannot happen between two copies of the same build)
        }
        else if (FartMonster.Instance != null) FartMonster.Instance.NetGone();

        int animals = r.ReadByte();
        for (int i = 0; i < animals && i < FartAnimal.All.Count; i++) FartAnimal.All[i].NetRead(r);

        if ((flags & 2) != 0 && !gm.GameOver && winner >= 0 && winner < gm.Competitors.Count)
            gm.NetGameOver(gm.Competitors[winner]);
    }
}

using UnityEngine;

/// <summary>
/// Dev tool: plays a two-player game by itself so the link can be tested
/// without two people ("-netBot" on the command line; never part of a normal game).
///
/// On the joining device it runs this device's player around the ground deck
/// from fart to fart, takes them to the goal and expels them there, and blasts
/// the host's player once. On the host it blasts the joiner once, can end the
/// match ("-netWinAt SECONDS") and start a new one. Both sides write a line of
/// "[NetLog]" every second - time, scores, backpacks, who is knocked out, what
/// the monster is doing - so that the two logs can be compared.
/// </summary>
public class NetBot : MonoBehaviour
{
    static int matches;

    PlayerController player;
    CharacterController cc;
    float nextHop, nextLog, nextExpel, winAt, blastAt, overSince = -1f, aloneSince = -1f;
    bool blasted, won, restarted, setMonster, blastedMonster, askedRestart;

    public static void Attach()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Player == null) return;
        var bot = gm.Player.gameObject.AddComponent<NetBot>();
        bot.player = gm.Player;
        bot.cc = gm.Player.GetComponent<CharacterController>();
        bot.winAt = Session.FlagValue("-netWinAt", -1f);
        bot.blastAt = Session.FlagValue("-netBlastAt", 10f);
        matches++;
        Debug.Log($"[NetLog] match {matches} begins as {Session.Current}");
    }

    void Hop(Vector3 target, float step)
    {
        Vector3 from = transform.position;
        Vector3 flat = new Vector3(target.x - from.x, 0f, target.z - from.z);
        if (flat.magnitude > step) flat = flat.normalized * step;
        cc.enabled = false;
        transform.position = new Vector3(from.x + flat.x, 0.95f, from.z + flat.z);
        if (flat.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(flat);
        cc.enabled = true;
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        float now = Time.unscaledTime;

        if (now >= nextLog)
        {
            nextLog = now + 1f;
            var sb = new System.Text.StringBuilder($"[NetLog] {Session.Current} t={gm.MatchTime:0.0} paused={gm.Paused} over={gm.GameOver}");
            if (gm.Winner != null) sb.Append($" winner={gm.IndexOf(gm.Winner)}");
            sb.Append(" |");
            foreach (var c in gm.Competitors)
                sb.Append($" {c.Score}/{c.Backpack}{(c.Stunned ? "S" : "")}{(c.Gone ? "G" : "")}");
            var m = FartMonster.Instance;
            sb.Append($" | monster={(m != null ? m.State.ToString() : "none")} dread={(LightingRig.Instance != null ? LightingRig.Instance.Dread : 0f):0.00}");
            sb.Append($" link={NetLink.Connected} states={(NetGame.Instance != null ? NetGame.Instance.States : 0)}");
            Debug.Log(sb.ToString());
        }

        if (gm.Paused) return;

        if (Session.IsHost || Session.Current == Session.Mode.Solo)
        {
            // the host's player mostly stands and watches
            if (!blasted && gm.MatchTime >= blastAt && gm.Competitors.Count > 1)
            {
                blasted = true;
                player.Collect(4);
                bool ok = player.BlastRival(gm.Competitors[1], player.blastCost, 3f);
                Debug.Log($"[NetLog] host blasted the joiner: {ok}");
            }
            if (winAt > 0f && !won && gm.MatchTime >= winAt && matches == 1)
            {
                won = true;
                for (int i = 0; i < 4 && !gm.GameOver; i++)
                {
                    player.Collect(player.Capacity);
                    while (!gm.GameOver && player.Expel()) { }
                }
                Debug.Log("[NetLog] host forced the end of the match");
            }
            // once, set the monster on the joiner so that its device gets the scare
            var beast = FartMonster.Instance;
            if (!setMonster && matches == 1 && beast != null && gm.MatchTime >= blastAt + 14f && gm.Competitors.Count > 1 &&
                !gm.Competitors[1].Stunned && !gm.Competitors[1].Gone && !gm.GameOver)
            {
                setMonster = true;
                var prey = gm.Competitors[1];
                beast.Summon(prey.transform.position - Vector3.up * 0.9f + Vector3.right * 1.2f, prey, FartMonster.Phase.Hunting);
                Debug.Log("[NetLog] host set the monster on the joiner");
            }
            if (gm.GameOver && won && !restarted && gm.MatchTime >= winAt + 8f)
            {
                restarted = true;           // (the joiner asks first; this is only the fallback)
                Debug.Log("[NetLog] host starts a new match");
                if (Session.IsHost) NetGame.HostRestart();
            }
            // left alone: start again, which must come up as an ordinary solo game
            if (Session.IsHost && !NetLink.Connected)
            {
                if (aloneSince < 0f) aloneSince = now;
                else if (now - aloneSince > 4f && !restarted)
                {
                    restarted = true;
                    Debug.Log("[NetLog] host restarts alone");
                    NetGame.HostRestart();
                }
            }
            return;
        }

        // the joiner asks for the new match, and drives the monster off once
        if (gm.GameOver)
        {
            if (overSince < 0f) overSince = now;
            else if (!askedRestart && now - overSince > 2.5f)
            {
                askedRestart = true;
                NetGame.SendAction(NetGame.ActRestart);
                Debug.Log("[NetLog] joiner asked for a new match");
            }
            return;
        }
        var monster = FartMonster.Instance;
        if (!blastedMonster && matches >= 2 && monster != null && monster.State == FartMonster.Phase.Hunting && player.Backpack >= player.blastCost && !player.Stunned)
        {
            blastedMonster = true;
            NetGame.SendAction(NetGame.ActBlastMonster);
            Debug.Log("[NetLog] joiner asked to blast the monster");
        }

        // the joiner's player works for its living
        if (gm.GameOver || player.Stunned || player.Scared) return;
        if (!blasted && gm.MatchTime >= blastAt + 6f && player.Backpack >= player.blastCost)
        {
            blasted = true;
            NetGame.SendAction(NetGame.ActBlastRival, 0);
            Debug.Log("[NetLog] joiner asked to blast the host");
        }
        if (now < nextHop) return;
        nextHop = now + 0.1f;

        if (player.Backpack >= 4)
        {
            Vector3 goal = Arena.GoalPos + new Vector3(-1.2f, 0f, 1.2f);
            Hop(goal, 0.9f);
            if (NetPuppet.InGoalBox(transform.position) && now >= nextExpel)
            {
                nextExpel = now + 0.15f;
                NetGame.SendAction(NetGame.ActExpel);
            }
            return;
        }
        if (NetPuppet.InGoalBox(transform.position) && player.Backpack > 0)
        {
            if (now >= nextExpel) { nextExpel = now + 0.15f; NetGame.SendAction(NetGame.ActExpel); }
            return;
        }
        FartPickup best = null;
        float bestDist = float.MaxValue;
        foreach (var p in gm.Pickups)
        {
            if (p == null || !p.Available || Arena.FloorOf(p.Home.y) != 0) continue;
            float d = (p.Home - transform.position).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = p; }
        }
        if (best != null) Hop(best.Home, 0.9f);
    }
}

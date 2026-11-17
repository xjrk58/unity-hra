using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Central coordinator: keeps the roster of competitors and pickups, decides the
/// winner, owns the HUD (<see cref="Hud"/>) and handles restart.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public int WinTarget = 15;
    public Transform GoalTransform;

    public bool GameOver { get; private set; }
    float gameOverAt;

    /// <summary>Seconds this match has been running (it does not run while waiting for the other player).</summary>
    public float MatchTime { get; set; }

    /// <summary>True while a two-player match waits for both devices to be ready.</summary>
    public bool Paused { get; set; }
    public Competitor Winner { get; private set; }

    readonly List<Competitor> competitors = new List<Competitor>();
    readonly List<FartPickup> pickups = new List<FartPickup>();

    public IReadOnlyList<Competitor> Competitors => competitors;
    public IReadOnlyList<FartPickup> Pickups => pickups;

    /// <summary>Position of a competitor in the roster (-1 if unknown).</summary>
    public int IndexOf(Competitor c) => competitors.IndexOf(c);

    /// <summary>The human player (null until registered).</summary>
    public PlayerController Player { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // everything has been built and registered by the bootstrap by now
        Hud.Build(this, Camera.main);
    }

    public void RegisterCompetitor(Competitor c)
    {
        if (c == null || competitors.Contains(c)) return;
        competitors.Add(c);
        if (c is PlayerController p) Player = p;
    }

    public void RegisterPickup(FartPickup p)
    {
        if (p != null && !pickups.Contains(p)) pickups.Add(p);
    }

    /// <summary>
    /// The pickup a bot should go for: the closest one, where changing deck
    /// counts as a long detour (you have to walk to a ramp and back).
    /// </summary>
    public FartPickup BestPickupFor(Vector3 from)
    {
        FartPickup best = null;
        float bestCost = float.MaxValue;
        int floor = Arena.FloorOf(from.y - 0.9f);
        foreach (var p in pickups)
        {
            if (p == null || !p.Available) continue;
            Vector3 d = p.transform.position - from;
            d.y = 0f;
            float cost = d.magnitude + 26f * Mathf.Abs(Arena.FloorOf(p.transform.position.y) - floor);
            if (cost < bestCost) { bestCost = cost; best = p; }
        }
        return best;
    }

    public void OnScoreChanged(Competitor c)
    {
        if (GameOver || Session.IsClient) return;       // on a joining device the host says who won
        if (c.Score >= WinTarget) Finish(c);
    }

    /// <summary>Joining device: the host says the match is over.</summary>
    public void NetGameOver(Competitor winner)
    {
        if (!GameOver) Finish(winner);
    }

    void Finish(Competitor winner)
    {
        GameOver = true;
        Winner = winner;
        gameOverAt = Time.unscaledTime;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        FartAudio.Fanfare();
    }

    void Update()
    {
        if (!Paused) MatchTime += Time.deltaTime;

        if (GameOver && GameInput.RestartDown(Time.unscaledTime - gameOverAt))
        {
            if (Session.IsClient) NetGame.SendAction(NetGame.ActRestart);    // the host starts the new match for both
            else if (Session.IsHost) NetGame.HostRestart();
            else SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        // the standalone game is full-screen with a captured mouse: Esc is the way out
        // (in the editor Esc just releases the mouse, as usual; on a phone it is Back)
        if (GameInput.QuitDown) Application.Quit();
    }
}

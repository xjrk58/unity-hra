using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Central coordinator: keeps the roster of competitors and pickups, decides the
/// winner, draws the HUD, and handles restart.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public int WinTarget = 15;
    public Transform GoalTransform;

    public bool GameOver { get; private set; }
    public Competitor Winner { get; private set; }

    readonly List<Competitor> competitors = new();
    readonly List<FartPickup> pickups = new();

    GUIStyle hudStyle, bigStyle, hintStyle;
    bool stylesReady;

    Competitor LocalPlayer => competitors.Find(c => c != null && c.IsPlayer);

    void Awake()
    {
        Instance = this;
    }

    public void RegisterCompetitor(Competitor c)
    {
        if (c != null && !competitors.Contains(c)) competitors.Add(c);
    }

    public void RegisterPickup(FartPickup p)
    {
        if (p != null && !pickups.Contains(p)) pickups.Add(p);
    }

    public FartPickup NearestAvailablePickup(Vector3 from)
    {
        FartPickup best = null;
        float bestSqr = float.MaxValue;
        foreach (var p in pickups)
        {
            if (p == null || !p.Available) continue;
            float d = (p.transform.position - from).sqrMagnitude;
            if (d < bestSqr) { bestSqr = d; best = p; }
        }
        return best;
    }

    public void OnScoreChanged(Competitor c)
    {
        if (GameOver) return;
        if (c.Score >= WinTarget)
        {
            GameOver = true;
            Winner = c;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    void Update()
    {
        if (GameOver && Input.GetKeyDown(KeyCode.R))
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void EnsureStyles()
    {
        if (stylesReady) return;
        hudStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
        hudStyle.normal.textColor = Color.white;
        bigStyle = new GUIStyle(GUI.skin.label)
        { fontSize = 48, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        bigStyle.normal.textColor = new Color(1f, 0.85f, 0.2f);
        hintStyle = new GUIStyle(GUI.skin.label)
        { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        hintStyle.normal.textColor = Color.white;
        stylesReady = true;
    }

    void OnGUI()
    {
        EnsureStyles();
        var player = LocalPlayer;
        const float pad = 12f;

        // Scoreboard
        GUI.Box(new Rect(pad, pad, 240, 34 + competitors.Count * 24), GUIContent.none);
        GUI.Label(new Rect(pad + 8, pad + 4, 230, 24), $"Cíl: dones {WinTarget} prdů", hudStyle);

        var sorted = new List<Competitor>(competitors);
        sorted.Sort((a, b) => b.Score.CompareTo(a.Score));
        for (int i = 0; i < sorted.Count; i++)
        {
            var c = sorted[i];
            string tag = c.IsPlayer ? "  (ty)" : (c.Stunned ? "  (vyřazen)" : "");
            GUI.Label(new Rect(pad + 8, pad + 32 + i * 24, 230, 24),
                $"{c.DisplayName}: {c.Score}{tag}", hudStyle);
        }

        // Player backpack + blast hint
        if (player != null)
        {
            GUI.Label(new Rect(pad, Screen.height - 40, 320, 30),
                $"Batůžek: {player.Backpack} / {player.Capacity}", hudStyle);
            GUI.Label(new Rect(pad, Screen.height - 66, 460, 30),
                "Pravé tlačítko: prdni po soupeři (−2 prdy, vyřadí na 10 s)", hudStyle);
        }

        // Crosshair
        const float cs = 5f;
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(Screen.width / 2f - cs / 2f, Screen.height / 2f - cs / 2f, cs, cs),
            Texture2D.whiteTexture);

        // Hint
        if (!GameOver && player != null)
        {
            string hint;
            if (player.InGoal && player.Backpack > 0)
                hint = "Drž levé tlačítko myši a vyprdni je do cíle!";
            else if (player.BackpackFull)
                hint = "Batůžek plný! Utíkej do oranžového cíle!";
            else if (player.Backpack == 0)
                hint = "Nasbírej zelené prdy do batůžku.";
            else
                hint = "Dones prdy do oranžového cíle a vyprdni je.";
            GUI.Label(new Rect(0, Screen.height - 104, Screen.width, 30), hint, hintStyle);
        }

        // Win banner
        if (GameOver && Winner != null)
        {
            GUI.Label(new Rect(0, Screen.height / 2f - 70, Screen.width, 60),
                $"{Winner.DisplayName} vyhrál!", bigStyle);
            GUI.Label(new Rect(0, Screen.height / 2f + 4, Screen.width, 30),
                "Stiskni R pro novou hru", hintStyle);
        }
    }
}

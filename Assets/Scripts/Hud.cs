using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The in-game HUD, built from code as a UI canvas that scales with the screen
/// (so it is the same size on a laptop and a 4K monitor): scoreboard, backpack,
/// crosshair, goal marker, hints, the "you got blasted" overlay and the win
/// banner. It is a screen-space overlay, so the scene's post-processing (bloom,
/// tonemapping) never touches the UI.
///
/// On a touch screen (<see cref="GameInput.Touch"/>) everything is drawn larger,
/// kept inside the display's safe area, the backpack moves out from under the
/// left thumb and the stick and the SKOK / PRD! buttons are added.
/// </summary>
public class Hud : MonoBehaviour
{
    const int UiLayer = 5;      // Unity's built-in "UI" layer
    static readonly Color Panel = new Color(0f, 0f, 0f, 0.5f);
    static readonly Color Orange = new Color(1f, 0.55f, 0.08f);
    static readonly Color Gas = new Color(0.5f, 0.9f, 0.2f);
    static readonly Color Gold = new Color(1f, 0.85f, 0.2f);

    class Row
    {
        public Image chip, bar;
        public Text name, score;
    }

    public static Hud Instance { get; private set; }

    GameManager gm;
    PlayerController player;
    Camera worldCam;
    RectTransform root, safe;
    Canvas canvas;
    Font font;
    bool touch;
    UnityEngine.Rect lastSafeArea;

    readonly List<Row> rows = new List<Row>();
    readonly List<Row> ranking = new List<Row>();     // final standings on the win banner
    readonly List<Competitor> sorted = new List<Competitor>();
    Text packTitle, hint, aimLabel, goalLabel, stunTitle, stunCount, winText;
    Text announceTitle, announceSub, huntedLabel, waitLabel;
    Image dangerImage, flashImage;
    float announceUntil, announceFor, flashUntil, flashFor;
    Color announceColor, flashColor;
    static Sprite dangerSprite;
    Image[] slots;
    RectTransform crossOuter, crossInner, goalMarker;
    Image crossInnerImage;
    GameObject stunOverlay, winBanner;
    const float BarWidth = 300f;

    // touch controls
    const float JumpRadius = 76f, BlastRadius = 86f;
    static readonly Vector2 StickHome = new Vector2(236f, 214f);
    RectTransform stickRing, stickKnob, jumpButton, blastButton;
    Image stickRingImage, stickKnobImage, jumpFill, blastFill, blastRim;
    Text blastLabel, blastCost;
    static Sprite discSprite, ringSprite;

    public static Hud Build(GameManager gm, Camera worldCam)
    {
        var go = new GameObject("Hud");
        Instance = go.AddComponent<Hud>();
        Instance.Init(gm, worldCam);
        return Instance;
    }

    // ------------------------------------------------------------------ construction

    void Init(GameManager manager, Camera cam)
    {
        gm = manager;
        worldCam = cam;
        player = gm.Player;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        touch = GameInput.Touch;

        // a screen-space overlay canvas: drawn after the scene and its post-processing
        var canvasGO = new GameObject("HudCanvas") { layer = UiLayer };
        canvasGO.transform.SetParent(transform, false);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        // a phone is held much closer than a monitor but is far smaller: draw everything larger
        scaler.referenceResolution = new Vector2(1920f, touch ? 780f : 1080f);
        scaler.matchWidthOrHeight = 1f;     // text keeps its size relative to screen height
        root = canvasGO.GetComponent<RectTransform>();

        // panels and buttons stay clear of notches and rounded corners
        safe = Rect("SafeArea", root, Center, Center, Vector2.zero, Vector2.zero);
        ApplySafeArea();

        BuildDanger();
        BuildStunOverlay();
        BuildScoreboard();
        BuildBackpack();
        BuildCrosshair();
        BuildGoalMarker();
        hint = touch
            ? Label("Hint", safe, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(1000f, 40f), 27, TextAnchor.MiddleCenter, Color.white)
            : Label("Hint", safe, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 176f), new Vector2(1400f, 44f), 30, TextAnchor.MiddleCenter, Color.white);
        if (touch) BuildTouchControls();
        BuildAnnouncements();
        BuildWinBanner();
        Refresh();
    }

    void ApplySafeArea()
    {
        UnityEngine.Rect area = Screen.safeArea;
        if (area == lastSafeArea) return;
        lastSafeArea = area;
        float w = Mathf.Max(1, Screen.width), h = Mathf.Max(1, Screen.height);
        safe.anchorMin = new Vector2(Mathf.Clamp01(area.xMin / w), Mathf.Clamp01(area.yMin / h));
        safe.anchorMax = new Vector2(Mathf.Clamp01(area.xMax / w), Mathf.Clamp01(area.yMax / h));
        safe.offsetMin = safe.offsetMax = Vector2.zero;
    }

    RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform)) { layer = UiLayer };
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    Image Box(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color color)
    {
        var img = Rect(name, parent, anchor, pivot, pos, size).gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    Text Label(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size,
        int fontSize, TextAnchor align, Color color)
    {
        var go = Rect(name, parent, anchor, pivot, pos, size).gameObject;
        var t = go.AddComponent<Text>();
        t.font = font;
        t.fontSize = fontSize;
        t.fontStyle = FontStyle.Bold;
        t.alignment = align;
        t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        var shadow = go.AddComponent<Shadow>();   // keeps text readable over bright sky
        shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
        shadow.effectDistance = new Vector2(2f, -2f);
        return t;
    }

    static readonly Vector2 TopLeft = new Vector2(0f, 1f), TopRight = Vector2.one, BottomLeft = Vector2.zero,
        BottomRight = new Vector2(1f, 0f), Center = new Vector2(0.5f, 0.5f);

    void BuildScoreboard()
    {
        int n = gm.Competitors.Count;
        var panel = Box("Scoreboard", safe, TopLeft, TopLeft, new Vector2(24f, -24f), new Vector2(380f, 58f + n * 48f), Panel);
        Label("Title", panel.transform, TopLeft, TopLeft, new Vector2(16f, -8f), new Vector2(350f, 36f), 26, TextAnchor.MiddleLeft, Color.white)
            .text = $"Cíl: dones {gm.WinTarget} prdů";
        for (int i = 0; i < n; i++)
        {
            float y = -52f - i * 48f;
            var row = new Row
            {
                chip = Box("Chip", panel.transform, TopLeft, TopLeft, new Vector2(16f, y - 8f), new Vector2(18f, 18f), Color.white),
                name = Label("Name", panel.transform, TopLeft, TopLeft, new Vector2(46f, y), new Vector2(260f, 34f), 24, TextAnchor.MiddleLeft, Color.white),
                score = Label("Score", panel.transform, TopLeft, TopLeft, new Vector2(300f, y), new Vector2(64f, 34f), 26, TextAnchor.MiddleRight, Color.white),
            };
            Box("BarBack", panel.transform, TopLeft, TopLeft, new Vector2(46f, y - 36f), new Vector2(BarWidth, 4f), new Color(1f, 1f, 1f, 0.2f));
            row.bar = Box("Bar", panel.transform, TopLeft, TopLeft, new Vector2(46f, y - 36f), new Vector2(0f, 4f), Color.white);
            rows.Add(row);
        }
    }

    void BuildBackpack()
    {
        if (player == null) return;
        const float slot = 34f, gap = 8f;
        // with a mouse: bottom left, with the key hints; on touch: top right, clear of both thumbs
        float width = Mathf.Max(touch ? 300f : 640f, 32f + player.Capacity * (slot + gap));
        Image panel = touch
            ? Box("Backpack", safe, TopRight, TopRight, new Vector2(-24f, -24f), new Vector2(width, 98f), Panel)
            : Box("Backpack", safe, BottomLeft, BottomLeft, new Vector2(24f, 24f), new Vector2(width, 128f), Panel);
        packTitle = Label("Title", panel.transform, TopLeft, TopLeft, new Vector2(16f, -8f), new Vector2(400f, 36f), 26, TextAnchor.MiddleLeft, Color.white);
        slots = new Image[player.Capacity];
        for (int i = 0; i < slots.Length; i++)
        {
            var back = Box("Slot", panel.transform, TopLeft, TopLeft, new Vector2(16f + i * (slot + gap), -48f), new Vector2(slot, slot), new Color(1f, 1f, 1f, 0.16f));
            slots[i] = Box("Fill", back.transform, Center, Center, Vector2.zero, new Vector2(slot - 6f, slot - 6f), Gas);
        }
        if (touch) return;
        var keys = Label("Keys", panel.transform, BottomLeft, BottomLeft, new Vector2(16f, 8f), new Vector2(width - 32f, 28f), 18, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.85f));
        keys.fontStyle = FontStyle.Normal;
        keys.text = $"Pravé tlačítko: prdni po soupeři (-{player.blastCost} prdy)   ·   Shift: sprint   ·   Esc: konec";
    }

    void BuildCrosshair()
    {
        crossOuter = Box("CrosshairEdge", root, Center, Center, Vector2.zero, new Vector2(10f, 10f), new Color(0f, 0f, 0f, 0.55f)).rectTransform;
        crossInnerImage = Box("Crosshair", root, Center, Center, Vector2.zero, new Vector2(6f, 6f), Color.white);
        crossInner = crossInnerImage.rectTransform;
        aimLabel = Label("Aim", root, Center, Center, new Vector2(0f, -150f), new Vector2(700f, 34f), 24, TextAnchor.MiddleCenter, Color.white);
    }

    void BuildGoalMarker()
    {
        goalMarker = Rect("GoalMarker", root, Center, Center, Vector2.zero, new Vector2(10f, 10f));
        var edge = Box("Edge", goalMarker, Center, Center, Vector2.zero, new Vector2(26f, 26f), new Color(0f, 0f, 0f, 0.6f));
        edge.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        var gem = Box("Gem", goalMarker, Center, Center, Vector2.zero, new Vector2(19f, 19f), Orange);
        gem.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        goalLabel = Label("Label", goalMarker, Center, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(520f, 32f), 23, TextAnchor.UpperCenter, Orange);
    }

    void BuildStunOverlay()
    {
        var img = Box("StunOverlay", root, Center, Center, Vector2.zero, Vector2.zero, new Color(0.33f, 0.52f, 0.08f, 0.45f));
        img.rectTransform.anchorMin = Vector2.zero;
        img.rectTransform.anchorMax = Vector2.one;
        stunOverlay = img.gameObject;
        stunTitle = Label("Title", img.transform, Center, Center, new Vector2(0f, 46f), new Vector2(1600f, 100f), 76, TextAnchor.MiddleCenter, Gold);
        stunCount = Label("Count", img.transform, Center, Center, new Vector2(0f, -30f), new Vector2(1200f, 44f), 32, TextAnchor.MiddleCenter, Color.white);
        stunOverlay.SetActive(false);
    }

    // The monster's corner of the HUD: darkness creeping in from the edges of the screen
    // as it closes in, and a flash when it strikes.
    void BuildDanger()
    {
        if (dangerSprite == null) dangerSprite = MakeEdgeGlow();
        dangerImage = Box("Danger", root, Center, Center, Vector2.zero, Vector2.zero, new Color(0.45f, 0f, 0f, 0f));
        dangerImage.sprite = dangerSprite;
        dangerImage.rectTransform.anchorMin = Vector2.zero;
        dangerImage.rectTransform.anchorMax = Vector2.one;
        dangerImage.enabled = false;
    }

    void BuildAnnouncements()
    {
        huntedLabel = Label("Hunted", safe, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(900f, 40f), 30, TextAnchor.MiddleCenter, new Color(1f, 0.2f, 0.1f));
        announceTitle = Label("Announce", root, Center, Center, new Vector2(0f, 250f), new Vector2(1700f, 110f), 88, TextAnchor.MiddleCenter, Color.white);
        announceSub = Label("AnnounceSub", root, Center, Center, new Vector2(0f, 176f), new Vector2(1500f, 46f), 34, TextAnchor.MiddleCenter, Color.white);
        announceTitle.gameObject.SetActive(false);
        announceSub.gameObject.SetActive(false);
        huntedLabel.gameObject.SetActive(false);

        // two-player games: shown until both devices have built the match
        waitLabel = Label("Wait", root, Center, Center, new Vector2(0f, 120f), new Vector2(1600f, 80f), 60, TextAnchor.MiddleCenter, Color.white);
        waitLabel.text = "Čekám na druhého hráče…";
        waitLabel.gameObject.SetActive(false);

        flashImage = Box("Flash", root, Center, Center, Vector2.zero, Vector2.zero, Color.clear);
        flashImage.rectTransform.anchorMin = Vector2.zero;
        flashImage.rectTransform.anchorMax = Vector2.one;
        flashImage.enabled = false;
    }

    /// <summary>Big words across the middle of the screen for a few seconds.</summary>
    public void Announce(string title, string sub, Color color, float seconds)
    {
        if (announceTitle == null) return;
        announceTitle.text = title;
        announceSub.text = sub;
        announceColor = color;
        announceFor = seconds;
        announceUntil = Time.time + seconds;
    }

    /// <summary>The whole screen flares in a colour and fades.</summary>
    public void Flash(Color color, float seconds)
    {
        if (flashImage == null) return;
        flashColor = color;
        flashFor = seconds;
        flashUntil = Time.time + seconds;
    }

    // Transparent in the middle, solid at the edges and most of all in the corners.
    static Sprite MakeEdgeGlow()
    {
        const int n = 128;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                float a = Mathf.Clamp01((Mathf.Sqrt(u * u + v * v) - 0.55f) / 0.75f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        var sprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    void RefreshMonster(bool over)
    {
        var monster = FartMonster.Instance;
        float now = Time.time;
        waitLabel.gameObject.SetActive(gm.Paused && !over);

        // announcement: slams in, holds, fades
        bool announcing = now < announceUntil && !over;
        announceTitle.gameObject.SetActive(announcing);
        announceSub.gameObject.SetActive(announcing && announceSub.text.Length > 0);
        if (announcing)
        {
            float left = announceUntil - now, age = announceFor - left;
            float a = Mathf.Clamp01(left / 0.6f);
            float pop = 1f + 0.35f * Mathf.Clamp01(1f - age / 0.18f);
            announceTitle.color = new Color(announceColor.r, announceColor.g, announceColor.b, a);
            announceTitle.rectTransform.localScale = new Vector3(pop, pop, 1f);
            announceSub.color = new Color(1f, 1f, 1f, a);
        }

        bool flashing = now < flashUntil;
        flashImage.enabled = flashing;
        if (flashing) flashImage.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0.75f * (flashUntil - now) / Mathf.Max(0.01f, flashFor));

        // being hunted: how far behind it is (but not where), and the dark closing in
        bool out_ = monster != null && monster.Visible && player != null && !over;
        bool hunted = out_ && monster.Target == player && !player.Stunned
                      && (monster.State == FartMonster.Phase.Hunting || monster.State == FartMonster.Phase.Emerging);
        huntedLabel.gameObject.SetActive(hunted);
        float danger = 0f;
        if (out_)
        {
            float dist = Vector3.Distance(player.transform.position, monster.transform.position + Vector3.up * 0.9f);
            if (hunted)
            {
                Set(huntedLabel, $"PŘÍŠERA TĚ LOVÍ!  {dist:0} m");
                float pulse = 0.75f + 0.25f * Mathf.Sin(now * 9f);
                huntedLabel.color = new Color(1f, 0.2f, 0.1f, pulse);
            }
            danger = Mathf.Clamp01(1f - (dist - 2f) / (hunted ? 16f : 8f));
            if (monster.State == FartMonster.Phase.Feeding && monster.Victim == player) danger = 1f;
        }
        dangerImage.enabled = danger > 0.01f;
        if (danger > 0.01f)
            dangerImage.color = new Color(0.42f, 0f, 0f, danger * (0.62f + 0.2f * Mathf.Sin(now * (4f + 8f * danger))));
    }

    void BuildWinBanner()
    {
        // winner on top, then everybody in finishing order, then how to play again
        int n = gm.Competitors.Count;
        const float rowH = 46f;
        float height = 150f + n * rowH + 118f;
        var img = Box("WinBanner", root, Center, Center, Vector2.zero, new Vector2(0f, height), new Color(0f, 0f, 0f, 0.62f));
        img.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        img.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        winBanner = img.gameObject;
        float top = height * 0.5f;
        winText = Label("Winner", img.transform, Center, Center, new Vector2(0f, top - 68f), new Vector2(1700f, 100f), 80, TextAnchor.MiddleCenter, Gold);

        var title = Label("RankTitle", img.transform, Center, Center, new Vector2(0f, top - 136f), new Vector2(600f, 30f), 22, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.75f));
        title.fontStyle = FontStyle.Normal;
        title.text = "Konečné pořadí";
        for (int i = 0; i < n; i++)
        {
            float y = top - 150f - (i + 0.5f) * rowH - 12f;
            ranking.Add(new Row
            {
                chip = Box("Chip", img.transform, Center, Center, new Vector2(-190f, y), new Vector2(22f, 22f), Color.white),
                name = Label("Name", img.transform, Center, new Vector2(0f, 0.5f), new Vector2(-160f, y), new Vector2(330f, 40f), 32, TextAnchor.MiddleLeft, Color.white),
                score = Label("Score", img.transform, Center, new Vector2(1f, 0.5f), new Vector2(300f, y), new Vector2(220f, 40f), 32, TextAnchor.MiddleRight, Color.white),
            });
            Label("Place", img.transform, Center, new Vector2(1f, 0.5f), new Vector2(-215f, y), new Vector2(80f, 40f), 32, TextAnchor.MiddleRight, Color.white)
                .text = (i + 1) + ".";
        }

        Label("Restart", img.transform, Center, Center, new Vector2(0f, -top + 68f), new Vector2(1200f, 44f), 30, TextAnchor.MiddleCenter, Color.white)
            .text = touch ? "Ťukni pro novou hru" : "Stiskni R pro novou hru";
        // the recordings are CC BY: the credit travels with the game
        var credit = Label("Credit", img.transform, Center, Center, new Vector2(0f, -top + 26f), new Vector2(1200f, 24f), 17, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.7f));
        credit.fontStyle = FontStyle.Normal;
        credit.text = "Zvuky prdů: „Farts - pieruja“, Yle Arkisto (Freesound), licence CC BY 3.0, sestříháno";
        winBanner.SetActive(false);
    }

    /// <summary>"1 prd", "3 prdy", "15 prdů".</summary>
    public static string Farts(int n) => n == 1 ? "1 prd" : n >= 2 && n <= 4 ? n + " prdy" : n + " prdů";

    // ------------------------------------------------------------------ touch controls

    void BuildTouchControls()
    {
        if (discSprite == null) discSprite = MakeDisc(0f);
        if (ringSprite == null) ringSprite = MakeDisc(0.9f);

        // the stick: sits bottom left until a thumb lands, then moves under it
        float r = GameInput.StickRadius / Mathf.Max(1f, Screen.height) * 780f;     // in canvas units
        stickRingImage = Round("StickRing", root, BottomLeft, Vector2.zero, r * 2f + 12f, ringSprite, new Color(1f, 1f, 1f, 0.35f));
        stickRing = stickRingImage.rectTransform;
        stickKnobImage = Round("StickKnob", root, BottomLeft, Vector2.zero, r * 0.95f, discSprite, new Color(1f, 1f, 1f, 0.35f));
        stickKnob = stickKnobImage.rectTransform;

        jumpFill = Round("Jump", safe, BottomRight, new Vector2(-128f, 122f), JumpRadius * 2f, discSprite, Panel);
        jumpButton = jumpFill.rectTransform;
        Round("Rim", jumpButton, Center, Vector2.zero, JumpRadius * 2f, ringSprite, new Color(1f, 1f, 1f, 0.6f));
        Label("Label", jumpButton, Center, Center, Vector2.zero, new Vector2(150f, 40f), 28, TextAnchor.MiddleCenter, Color.white).text = "SKOK";

        blastFill = Round("Blast", safe, BottomRight, new Vector2(-306f, 226f), BlastRadius * 2f, discSprite, Panel);
        blastButton = blastFill.rectTransform;
        blastRim = Round("Rim", blastButton, Center, Vector2.zero, BlastRadius * 2f, ringSprite, Color.white);
        blastLabel = Label("Label", blastButton, Center, Center, new Vector2(0f, 12f), new Vector2(170f, 44f), 34, TextAnchor.MiddleCenter, Color.white);
        blastLabel.text = "PRD!";
        blastCost = Label("Cost", blastButton, Center, Center, new Vector2(0f, -26f), new Vector2(170f, 28f), 20, TextAnchor.MiddleCenter, Color.white);
        blastCost.text = player != null ? $"−{player.blastCost} prdy" : "";
    }

    Image Round(string name, Transform parent, Vector2 anchor, Vector2 pos, float diameter, Sprite sprite, Color color)
    {
        var img = Box(name, parent, anchor, Center, pos, new Vector2(diameter, diameter), color);
        img.sprite = sprite;
        return img;
    }

    // A white anti-aliased disc; with a hole of the given relative radius it is a ring.
    static Sprite MakeDisc(float hole)
    {
        const int n = 160;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };
        var px = new Color32[n * n];
        float c = (n - 1) * 0.5f, r = n * 0.5f - 1.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = Mathf.Clamp01(r - d);
                if (hole > 0f) a *= Mathf.Clamp01(d - r * hole);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        var sprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    void RefreshTouchControls(bool playing)
    {
        float scale = Mathf.Max(0.01f, canvas.scaleFactor);
        stickRing.gameObject.SetActive(playing);
        stickKnob.gameObject.SetActive(playing);
        jumpButton.gameObject.SetActive(playing);
        blastButton.gameObject.SetActive(playing);

        // tell the input where the buttons are (a little generous: thumbs are not precise)
        GameInput.JumpZone = playing ? ZoneOf(jumpButton, JumpRadius * 1.15f, scale) : default;
        GameInput.BlastZone = playing ? ZoneOf(blastButton, BlastRadius * 1.15f, scale) : default;
        if (!playing) return;

        bool held = GameInput.StickHeld;
        Vector2 home = new Vector2(safe.anchorMin.x * root.rect.width, safe.anchorMin.y * root.rect.height) + StickHome;
        stickRing.anchoredPosition = held ? GameInput.StickOrigin / scale : home;
        stickKnob.anchoredPosition = held ? GameInput.StickPosition / scale : home;
        stickRingImage.color = new Color(1f, 1f, 1f, held ? 0.55f : 0.3f);
        stickKnobImage.color = new Color(1f, 1f, 1f, held ? 0.7f : 0.3f);

        jumpFill.color = GameInput.JumpHeld ? new Color(1f, 1f, 1f, 0.45f) : Panel;

        bool armed = player.CanBlast;
        blastFill.color = armed
            ? new Color(Orange.r, Orange.g, Orange.b, GameInput.BlastHeld ? 1f : 0.85f)
            : new Color(0f, 0f, 0f, 0.3f);
        float a = armed ? 1f : 0.4f;
        blastRim.color = new Color(1f, 1f, 1f, armed ? 0.95f : 0.3f);
        blastLabel.color = blastCost.color = new Color(1f, 1f, 1f, a);
    }

    GameInput.Zone ZoneOf(RectTransform rt, float radius, float scale)
    {
        Camera uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        return new GameInput.Zone
        {
            centre = RectTransformUtility.WorldToScreenPoint(uiCam, rt.position),
            radius = radius * scale,
        };
    }

    // ------------------------------------------------------------------ per frame

    void LateUpdate() => Refresh();

    /// <summary>Brings every element up to date with the game state.</summary>
    public void Refresh()
    {
        if (gm == null) return;
        ApplySafeArea();
        bool over = gm.GameOver;
        bool stunned = player != null && player.Stunned && !over;

        // scoreboard, leader first
        sorted.Clear();
        sorted.AddRange(gm.Competitors);
        sorted.Sort((a, b) =>
        {
            if (a.Score != b.Score) return b.Score.CompareTo(a.Score);
            if (a.Backpack != b.Backpack) return b.Backpack.CompareTo(a.Backpack);   // tie: whoever carries more is closer
            return gm.IndexOf(a).CompareTo(gm.IndexOf(b));
        });
        for (int i = 0; i < rows.Count && i < sorted.Count; i++)
        {
            var c = sorted[i];
            var r = rows[i];
            r.chip.color = c.Tint;
            r.bar.color = Color.Lerp(c.Tint, Color.white, 0.25f);
            r.bar.rectTransform.sizeDelta = new Vector2(BarWidth * Mathf.Clamp01(c.Score / (float)gm.WinTarget), 4f);
            string who = c.DisplayName;
            if (c.Stunned) who += $"  – vyřazen {Mathf.CeilToInt(c.StunRemaining)} s";
            Set(r.name, who);
            Set(r.score, c.Score.ToString());
        }

        if (player != null && slots != null)
        {
            Set(packTitle, $"Batůžek  {player.Backpack} / {player.Capacity}");
            for (int i = 0; i < slots.Length; i++) slots[i].enabled = i < player.Backpack;
        }

        stunOverlay.SetActive(stunned);
        if (stunned)
        {
            bool eaten = player.StunCause == "monster";
            Set(stunTitle, eaten ? "Příšera tě dostala!" : "Dostal jsi prdem!");
            Set(stunCount, (eaten ? "Sežrala ti všechny prdy. " : "") + $"Vzpamatuješ se za {Mathf.CeilToInt(player.StunRemaining)} s");
        }
        RefreshMonster(over);

        winBanner.SetActive(over && gm.Winner != null);
        if (over && gm.Winner != null)
        {
            Set(winText, gm.Winner.IsPlayer ? "Vyhrál jsi!" : $"{gm.Winner.DisplayName} vyhrál!");
            for (int i = 0; i < ranking.Count && i < sorted.Count; i++)
            {
                var c = sorted[i];
                var r = ranking[i];
                r.chip.color = c.Tint;
                r.name.color = r.score.color = c.IsPlayer ? Gold : Color.white;
                Set(r.name, c.DisplayName);
                Set(r.score, Farts(c.Score));
            }
        }

        bool playing = player != null && !over && !stunned && !player.Scared;
        crossOuter.gameObject.SetActive(playing);
        crossInner.gameObject.SetActive(playing);
        aimLabel.gameObject.SetActive(playing && (player.AimTarget != null || player.AimMonster));
        hint.gameObject.SetActive(playing);
        if (playing)
        {
            bool armed = player.CanBlast;
            float s = armed ? 12f : 6f;
            crossInner.sizeDelta = new Vector2(s, s);
            crossOuter.sizeDelta = new Vector2(s + 4f, s + 4f);
            crossInnerImage.color = armed ? Orange : Color.white;
            if (player.AimMonster)
            {
                aimLabel.color = armed ? Orange : Color.white;
                Set(aimLabel, armed ? "PŘÍŠERA – zažeň ji prdem!" : $"PŘÍŠERA (na zahnání potřebuješ {player.blastCost} prdy)");
            }
            else if (player.AimTarget != null)
            {
                aimLabel.color = armed ? Orange : Color.white;
                Set(aimLabel, armed
                    ? $"{player.AimTarget.DisplayName} – prdni po něm!"
                    : $"{player.AimTarget.DisplayName} (na prdnutí potřebuješ {player.blastCost} prdy)");
            }

            string text;
            if (player.InGoal && player.Backpack > 0) text = touch ? "Stůj v cíli – prdy se vyprdí samy!" : "Drž levé tlačítko myši a vyprdni je do cíle!";
            else if (player.BackpackFull) text = "Batůžek plný! Utíkej do oranžového cíle!";
            else if (player.Backpack == 0) text = "Nasbírej zelené prdy do batůžku.";
            else text = "Dones prdy do oranžového cíle a vyprdni je.";
            Set(hint, text);
        }

        UpdateGoalMarker(playing);
        if (touch) RefreshTouchControls(playing);
    }

    // Where the goal is, once there is something to deliver: a marker with the
    // distance, pinned to the screen edge when the goal is out of view.
    void UpdateGoalMarker(bool playing)
    {
        bool show = playing && gm.GoalTransform != null && player.Backpack > 0 && !player.InGoal && worldCam != null;
        goalMarker.gameObject.SetActive(show);
        if (!show) return;

        Vector3 vp = worldCam.WorldToViewportPoint(gm.GoalTransform.position + Vector3.up * 2.4f);
        if (vp.z < 0f) { vp.x = 1f - vp.x; vp.y = 0f; }     // behind us: mirror and drop to the bottom edge
        float w = root.rect.width, h = root.rect.height;
        float x = Mathf.Clamp((vp.x - 0.5f) * w, -w * 0.5f + 150f, w * 0.5f - 150f);
        float y = Mathf.Clamp((vp.y - 0.5f) * h, -h * 0.5f + 250f, h * 0.5f - 70f);
        goalMarker.anchoredPosition = new Vector2(x, y);

        float dist = Vector3.Distance(player.transform.position, gm.GoalTransform.position);
        int deck = Arena.FloorOfFeet(player.transform.position.y - 0.9f);
        string down = deck == 0 ? "" : deck == 1 ? "  (o patro níž)" : $"  (o {deck} patra níž)";
        Set(goalLabel, $"CÍL  {dist:0} m{down}");
    }

    static void Set(Text t, string value)
    {
        if (t.text != value) t.text = value;
    }
}

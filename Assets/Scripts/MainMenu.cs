using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The title screen: play alone, or play with somebody else on a second device.
/// One device sets the game up ("založit") and waits - on the Wi-Fi it is on
/// and, on an Android phone, on Bluetooth as well. The other joins: over Wi-Fi
/// by picking the game from the list of those it can hear or by tapping in the
/// short code the host shows (the last number of its network address); over
/// Bluetooth by picking the host from its list of paired phones. Nobody has to
/// type an address.
///
/// The menu is drawn over the empty arena with a slowly circling camera. Making
/// a choice reloads the scene in that mode (<see cref="Session"/>).
/// </summary>
public class MainMenu : MonoBehaviour
{
    enum Page { Main, Hosting, Joining }

    static readonly Color Gold = new Color(1f, 0.85f, 0.2f);
    static readonly Color ButtonColor = new Color(0.10f, 0.13f, 0.10f, 0.88f);
    static readonly Color Accent = new Color(0.36f, 0.62f, 0.14f, 0.95f);

    Font font;
    RectTransform root;
    GameObject mainPanel, hostPanel, joinPanel;
    Text hostCode, hostAddress, hostHint, hostBluetooth, joinCode, joinStatus, joinSearching, joinListTitle, notice;
    const int Entries = 5;      // games heard on Wi-Fi first, then paired phones (Bluetooth)
    readonly Button[] hostButtons = new Button[Entries];
    readonly Text[] hostLabels = new Text[Entries];
    readonly string[] hostAddresses = new string[Entries];
    readonly bool[] hostIsBluetooth = new bool[Entries];
    System.Collections.Generic.List<BluetoothLink.Device> paired = new System.Collections.Generic.List<BluetoothLink.Device>();
    Page page;
    string typed = "";
    bool joinPending;
    float retryAt, nextBluetoothCheck;

    public static void Build()
    {
        new GameObject("MainMenu").AddComponent<MainMenu>().Init();
    }

    // ------------------------------------------------------------------ construction

    void Init()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;

        var canvasGO = new GameObject("MenuCanvas") { layer = 5 };
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        canvasGO.AddComponent<GraphicRaycaster>();
        root = canvasGO.GetComponent<RectTransform>();

        if (FindAnyObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.transform.SetParent(transform, false);
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        var shade = Box("Shade", root, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.55f));
        shade.rectTransform.anchorMin = Vector2.zero;
        shade.rectTransform.anchorMax = Vector2.one;

        BuildMain();
        BuildHosting();
        BuildJoining();
        Show(Page.Main);

        // straight into a two-player game from the command line (tests, shortcuts)
        if (Session.HasFlag("-menuJoin")) StartJoining();       // (for screenshots of that page)
        else if (Session.AutoHost) StartHosting();
        else if (Session.AutoJoin != "")
        {
            Show(Page.Joining);
            NetLink.Listen();
            Join(Session.AutoJoin);
        }
    }

    RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    Image Box(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
    {
        var img = Rect(name, parent, pos, size).gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    Text Label(string text, Transform parent, Vector2 pos, Vector2 size, int fontSize, Color color)
    {
        var go = Rect("Label", parent, pos, size).gameObject;
        var t = go.AddComponent<Text>();
        t.font = font;
        t.fontSize = fontSize;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        t.text = text;
        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
        shadow.effectDistance = new Vector2(2f, -2f);
        return t;
    }

    Button Key(string text, Transform parent, Vector2 pos, Vector2 size, int fontSize, Color color, UnityEngine.Events.UnityAction onClick, out Text label)
    {
        var img = Box("Button", parent, pos, size, color);
        img.raycastTarget = true;
        var b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        var colors = b.colors;
        colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        b.colors = colors;
        b.onClick.AddListener(onClick);
        label = Label(text, img.transform, Vector2.zero, size, fontSize, Color.white);
        return b;
    }

    Button Key(string text, Transform parent, Vector2 pos, Vector2 size, int fontSize, Color color, UnityEngine.Events.UnityAction onClick) =>
        Key(text, parent, pos, size, fontSize, color, onClick, out _);

    void BuildMain()
    {
        mainPanel = Rect("Main", root, Vector2.zero, Vector2.zero).gameObject;
        var p = mainPanel.transform;
        Label("PRDIVÝ ZÁVOD", p, new Vector2(0f, 350f), new Vector2(1600f, 150f), 124, Gold);
        Label("Nasbírej prdy do batůžku a vyprdni je v cíli dřív než ostatní.", p, new Vector2(0f, 248f), new Vector2(1600f, 50f), 32, Color.white);
        var size = new Vector2(720f, 126f);
        Key("Hrát sám", p, new Vector2(0f, 110f), size, 46, Accent, PlaySolo);
        Key("Hra pro dva: založit", p, new Vector2(0f, -40f), size, 46, ButtonColor, StartHosting);
        Key("Hra pro dva: připojit se", p, new Vector2(0f, -190f), size, 46, ButtonColor, StartJoining);
        notice = Label("", p, new Vector2(0f, -320f), new Vector2(1600f, 44f), 32, new Color(1f, 0.6f, 0.2f));
        if (!Application.isMobilePlatform)
            Label("Esc: konec", p, new Vector2(0f, -470f), new Vector2(600f, 36f), 24, new Color(1f, 1f, 1f, 0.7f)).fontStyle = FontStyle.Normal;
    }

    void BuildHosting()
    {
        hostPanel = Rect("Hosting", root, Vector2.zero, Vector2.zero).gameObject;
        var p = hostPanel.transform;
        Label("Čekám na druhého hráče…", p, new Vector2(0f, 330f), new Vector2(1600f, 90f), 64, Color.white);
        Label("Kód hry", p, new Vector2(0f, 215f), new Vector2(600f, 50f), 38, new Color(1f, 1f, 1f, 0.85f));
        hostCode = Label("", p, new Vector2(0f, 60f), new Vector2(900f, 240f), 220, Gold);
        hostHint = Label("", p, new Vector2(0f, -140f), new Vector2(1700f, 90f), 31, Color.white);
        hostAddress = Label("", p, new Vector2(0f, -232f), new Vector2(1200f, 36f), 24, new Color(1f, 1f, 1f, 0.7f));
        hostAddress.fontStyle = FontStyle.Normal;
        hostBluetooth = Label("", p, new Vector2(0f, -278f), new Vector2(1700f, 40f), 27, new Color(0.6f, 0.8f, 1f));
        Key("Zpět", p, new Vector2(0f, -384f), new Vector2(420f, 104f), 42, ButtonColor, Back);
    }

    void BuildJoining()
    {
        joinPanel = Rect("Joining", root, Vector2.zero, Vector2.zero).gameObject;
        var p = joinPanel.transform;
        Label("Připojit se ke hře", p, new Vector2(0f, 430f), new Vector2(1400f, 90f), 62, Color.white);

        // left: games heard on this network, then (on a phone) the phones paired with this one
        joinListTitle = Label("Nalezené hry", p, new Vector2(-400f, 330f), new Vector2(620f, 46f), 34, new Color(1f, 1f, 1f, 0.85f));
        joinSearching = Label("Hledám hry na této Wi-Fi…", p, new Vector2(-400f, 236f), new Vector2(620f, 44f), 28, new Color(1f, 1f, 1f, 0.75f));
        joinSearching.fontStyle = FontStyle.Normal;
        for (int i = 0; i < hostButtons.Length; i++)
        {
            int index = i;
            hostButtons[i] = Key("", p, new Vector2(-400f, 236f - i * 96f), new Vector2(600f, 84f), 30, Accent, () => JoinEntry(index), out hostLabels[i]);
            hostButtons[i].gameObject.SetActive(false);
        }
        Key("Zpět", p, new Vector2(-400f, -262f), new Vector2(420f, 104f), 42, ButtonColor, Back);

        // right: or tap in the code the host shows
        Label("Nebo zadej kód hry", p, new Vector2(400f, 330f), new Vector2(560f, 46f), 34, new Color(1f, 1f, 1f, 0.85f));
        Box("CodeBox", p, new Vector2(400f, 228f), new Vector2(470f, 100f), new Color(0f, 0f, 0f, 0.6f));
        joinCode = Label("", p, new Vector2(400f, 228f), new Vector2(470f, 100f), 72, Gold);
        string[] keys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "←", "0", "OK" };
        for (int i = 0; i < keys.Length; i++)
        {
            string k = keys[i];
            var pos = new Vector2(400f + (i % 3 - 1) * 162f, 104f - (i / 3) * 120f);
            Key(k, p, pos, new Vector2(146f, 104f), 48, k == "OK" ? Accent : ButtonColor, () => Press(k));
        }
        joinStatus = Label("", p, new Vector2(0f, -400f), new Vector2(1700f, 44f), 32, new Color(1f, 0.85f, 0.4f));
    }

    void Show(Page next)
    {
        page = next;
        mainPanel.SetActive(next == Page.Main);
        hostPanel.SetActive(next == Page.Hosting);
        joinPanel.SetActive(next == Page.Joining);
        if (next == Page.Main)
        {
            notice.text = Session.Notice;
            Session.Notice = "";
        }
    }

    // ------------------------------------------------------------------ choices

    void PlaySolo()
    {
        NetLink.Close();
        Session.Current = Session.Mode.Solo;
        Session.Reload();
    }

    void StartHosting()
    {
        if (!NetLink.Host())
        {
            Session.Notice = NetLink.Problem;
            Show(Page.Main);
            return;
        }
        string address = NetLink.LocalAddress();
        bool online = address != "";
        hostCode.text = online ? NetLink.CodeOf(address) : "?";
        hostHint.text = online
            ? "Druhý hráč zvolí „Hra pro dva: připojit se“ a vybere tuto hru, nebo zadá tento kód.\nObě zařízení musí být na stejné Wi-Fi."
            : "Toto zařízení není připojené k Wi-Fi. Připoj je a zkus to znovu.";
        hostAddress.text = online ? $"(adresa tohoto zařízení: {address})" : "";
        hostBluetooth.text = "";
        nextBluetoothCheck = 0f;
        BluetoothLink.AskPermission();      // (phones only; the answer is picked up in Update)
        Show(Page.Hosting);
    }

    void StartJoining()
    {
        typed = "";
        joinCode.text = "";
        joinStatus.text = "";
        joinPending = false;
        paired.Clear();
        nextBluetoothCheck = 0f;
        NetLink.Listen();
        BluetoothLink.AskPermission();
        joinListTitle.text = BluetoothLink.Supported ? "Hry na Wi-Fi a spárované telefony" : "Nalezené hry";
        Show(Page.Joining);
    }

    void JoinEntry(int index)
    {
        if (!hostIsBluetooth[index])
        {
            Join(hostAddresses[index]);
            return;
        }
        if (NetLink.Connecting || NetLink.Connected) return;
        NetLink.JoinBluetooth(hostAddresses[index]);
        joinPending = true;
        joinStatus.text = "Připojuji přes Bluetooth… (může to trvat několik vteřin)";
    }

    void Back()
    {
        NetLink.Close();
        Session.AutoHost = false;
        Session.AutoJoin = "";
        Show(Page.Main);
    }

    void Press(string key)
    {
        if (key == "OK")
        {
            string address = NetLink.AddressFor(typed);
            if (address == "") joinStatus.text = NetLink.LocalAddress() == "" ? "Toto zařízení není připojené k Wi-Fi." : "Kód je číslo od 1 do 254.";
            else Join(address);
            return;
        }
        if (key == "←") typed = typed.Length > 0 ? typed.Substring(0, typed.Length - 1) : "";
        else if (typed.Length < 3) typed += key;
        joinCode.text = typed;
    }

    void Join(string address)
    {
        if (string.IsNullOrEmpty(address) || NetLink.Connecting || NetLink.Connected) return;
        NetLink.Join(address);
        joinPending = true;
        joinStatus.text = "Připojuji…";
    }

    // ------------------------------------------------------------------ every frame

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (page == Page.Main) Application.Quit();
            else Back();
            return;
        }

        float now = Time.realtimeSinceStartup;
        if (page == Page.Hosting)
        {
            if (NetLink.Connected)
            {
                Session.Current = Session.Mode.Host;    // the other player is here: build the match
                Session.Reload();
                return;
            }
            // a phone also waits on Bluetooth, as soon as that is on and allowed
            if (BluetoothLink.Supported && now >= nextBluetoothCheck)
            {
                nextBluetoothCheck = now + 1f;
                if (!BluetoothLink.Allowed) hostBluetooth.text = "Bluetooth: povol hře přístup k zařízením v okolí, nebo hrajte přes Wi-Fi.";
                else if (NetLink.HostBluetooth()) hostBluetooth.text = $"Bluetooth: na druhém, spárovaném telefonu vyber v seznamu „{BluetoothLink.LocalName}“.";
                else hostBluetooth.text = "Bluetooth je vypnutý – přes Wi-Fi to jde i bez něj.";
            }
            return;
        }
        if (page != Page.Joining) return;

        // the phones paired with this one (they may or may not be waiting with a game)
        if (BluetoothLink.Supported && now >= nextBluetoothCheck)
        {
            nextBluetoothCheck = now + 2f;
            paired = BluetoothLink.Ready ? BluetoothLink.Paired() : new System.Collections.Generic.List<BluetoothLink.Device>();
        }

        // the games this device can hear on Wi-Fi, then the paired phones
        int wifi = Mathf.Min(NetLink.Hosts.Count, 3);
        int shown = 0;
        for (int i = 0; i < wifi; i++, shown++)
        {
            hostAddresses[shown] = NetLink.Hosts[i].address;
            hostIsBluetooth[shown] = false;
            hostLabels[shown].text = $"{NetLink.Hosts[i].name}  (kód {NetLink.CodeOf(NetLink.Hosts[i].address)})";
        }
        for (int i = 0; i < paired.Count && shown < Entries; i++, shown++)
        {
            hostAddresses[shown] = paired[i].address;
            hostIsBluetooth[shown] = true;
            hostLabels[shown].text = "Bluetooth: " + paired[i].name;
        }
        for (int i = 0; i < Entries; i++) hostButtons[i].gameObject.SetActive(i < shown);
        joinSearching.gameObject.SetActive(shown == 0);

        if (NetLink.Connected)
        {
            joinStatus.text = "Připojeno. Čekám, až hostitel spustí hru…";
            while (NetLink.Receive(out byte type, out var reader))
            {
                if (type != NetGame.MsgStart) continue;
                Session.ReadStart(reader);
                Session.Current = Session.Mode.Client;
                Session.Reload();
                return;
            }
        }
        else if (!NetLink.Connecting && joinPending)
        {
            joinPending = false;
            joinStatus.text = NetLink.Problem;
            retryAt = Time.realtimeSinceStartup + 2f;
        }
        else if (!NetLink.Connecting && Session.AutoJoin != "" && Time.realtimeSinceStartup >= retryAt)
        {
            Join(Session.AutoJoin);         // started from the command line: keep knocking until the host is up
        }
    }
}

/// <summary>The camera behind the title screen: circles the arena from above.</summary>
public class MenuOrbit : MonoBehaviour
{
    void LateUpdate()
    {
        float a = Time.unscaledTime * 0.11f;
        Vector3 at = new Vector3(Mathf.Cos(a) * 36f, 17f, Mathf.Sin(a) * 36f);
        transform.SetPositionAndRotation(at, Quaternion.LookRotation(new Vector3(0f, 7f, 0f) - at, Vector3.up));
    }
}

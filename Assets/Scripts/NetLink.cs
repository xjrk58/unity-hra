using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

/// <summary>
/// The wire between two devices playing together: small length-prefixed
/// messages over one connection. That connection is either TCP on the same
/// Wi-Fi (with a UDP beacon so a joining device can find the host without
/// anybody typing an address) or, between two paired Android phones, a
/// Bluetooth serial line (<see cref="BluetoothLink"/>). A host waits on both at
/// once and takes whichever arrives first; the game above cannot tell which it got.
///
/// Nothing here blocks: <see cref="Tick"/> is called every frame (by a small
/// object that survives scene loads) and accepts, connects, reads and notices
/// a dropped line. A solo game never touches any of this and opens no sockets.
/// </summary>
public static class NetLink
{
    public const int Port = 47800;          // TCP: the game
    public const int BeaconPort = 47801;    // UDP: "a game is waiting here"
    const string Magic = "PRDZ1";

    public enum Role { None, Host, Client }

    /// <summary>A host heard on the network.</summary>
    public class Found
    {
        public string address, name;
        public float seen;
    }

    public static Role CurrentRole { get; private set; }

    /// <summary>True while the two devices are talking.</summary>
    public static bool Connected { get; private set; }

    /// <summary>True from the moment a join is started until it succeeds or fails.</summary>
    public static bool Connecting { get; private set; }

    /// <summary>Why the last join failed or the line dropped (empty if it did not).</summary>
    public static string Problem { get; private set; } = "";

    /// <summary>Hosts announcing themselves, freshest first (joining devices only).</summary>
    public static readonly List<Found> Hosts = new List<Found>();

    /// <summary>True while the two devices are talking over Bluetooth rather than Wi-Fi.</summary>
    public static bool OverBluetooth => Connected && viaBluetooth;

    /// <summary>True while a host is also waiting for a paired phone on Bluetooth.</summary>
    public static bool BluetoothHosting => bluetoothHosting;

    static Socket listener, socket, beacon;
    static bool viaBluetooth, bluetoothHosting, bluetoothJoining;
    static readonly byte[] inbox = new byte[1 << 17];
    static int inboxLen, readPos;
    static readonly byte[] chunk = new byte[1 << 15];
    static readonly MemoryStream outBuffer = new MemoryStream(1024);
    static readonly BinaryWriter outWriter = new BinaryWriter(outBuffer, Encoding.UTF8);
    static float connectStarted, nextBeacon;
    static Runner runner;

    // keeps the link alive across scene loads and closes it when the game quits
    class Runner : MonoBehaviour
    {
        void Update() => Tick();
        void OnApplicationQuit() => Close();
    }

    static void EnsureRunner()
    {
        if (runner != null) return;
        var go = new GameObject("NetLink");
        UnityEngine.Object.DontDestroyOnLoad(go);
        runner = go.AddComponent<Runner>();
        Application.runInBackground = true;     // a window without focus must keep answering
    }

    static void Log(string what) => Debug.Log("[Net] " + what);

    // ------------------------------------------------------------------ starting and stopping

    /// <summary>Wait for the other device to join, and announce the game on the network.</summary>
    public static bool Host()
    {
        Close();
        EnsureRunner();
        try
        {
            listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            listener.Bind(new IPEndPoint(IPAddress.Any, Port));
            listener.Listen(1);
            listener.Blocking = false;
        }
        catch (Exception e)
        {
            Problem = "Hru se nepodařilo založit (" + e.Message + ")";
            Log("host failed: " + e);
            Close();
            return false;
        }
        try
        {
            beacon = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            beacon.EnableBroadcast = true;
            beacon.Blocking = false;
        }
        catch (Exception e) { Log("no beacon: " + e.Message); beacon = null; }   // joining by code still works

        CurrentRole = Role.Host;
        Problem = "";
        Log($"hosting on {LocalAddress()}:{Port}");
        return true;
    }

    /// <summary>
    /// Host: also wait for a paired phone on Bluetooth (call after <see cref="Host"/>,
    /// once Bluetooth is on and allowed). False where there is no Bluetooth.
    /// </summary>
    public static bool HostBluetooth()
    {
        if (CurrentRole != Role.Host || Connected) return false;
        if (bluetoothHosting) return true;
        if (!BluetoothLink.Supported || !BluetoothLink.Ready) return false;
        bluetoothHosting = BluetoothLink.Host();
        if (bluetoothHosting) Log("also hosting on Bluetooth as " + BluetoothLink.LocalName);
        return bluetoothHosting;
    }

    /// <summary>Join the game hosted by a paired phone, over Bluetooth. Watch <see cref="Connected"/> and <see cref="Problem"/>.</summary>
    public static void JoinBluetooth(string address)
    {
        CloseSocket(ref socket);
        CloseSocket(ref listener);
        EnsureRunner();
        Connected = false;
        viaBluetooth = false;
        Problem = "";
        inboxLen = readPos = 0;
        if (!BluetoothLink.Join(address))
        {
            Problem = "Bluetooth není k dispozici";
            return;
        }
        CurrentRole = Role.Client;
        Connecting = true;
        bluetoothJoining = true;
        connectStarted = Time.realtimeSinceStartup;
        Log("joining over Bluetooth");
    }

    /// <summary>Start listening for hosts announcing themselves (the join screen).</summary>
    public static void Listen()
    {
        Close();
        EnsureRunner();
        Hosts.Clear();
        try
        {
            beacon = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            beacon.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            beacon.Bind(new IPEndPoint(IPAddress.Any, BeaconPort));
            beacon.Blocking = false;
        }
        catch (Exception e) { Log("cannot listen for hosts: " + e.Message); beacon = null; }
    }

    /// <summary>Join the host at <paramref name="address"/>. Watch <see cref="Connected"/> and <see cref="Problem"/>.</summary>
    public static void Join(string address)
    {
        CloseSocket(ref socket);
        CloseSocket(ref listener);
        CloseBluetooth();
        EnsureRunner();
        Connected = false;
        Problem = "";
        inboxLen = readPos = 0;
        try
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Blocking = false;
            try { socket.Connect(new IPEndPoint(IPAddress.Parse(address), Port)); }
            catch (SocketException e) when (e.SocketErrorCode == SocketError.WouldBlock || e.SocketErrorCode == SocketError.InProgress) { }
            CurrentRole = Role.Client;
            Connecting = true;
            connectStarted = Time.realtimeSinceStartup;
            Log("joining " + address);
        }
        catch (Exception e)
        {
            Problem = "Adresa „" + address + "“ není dosažitelná";
            Log("join failed: " + e.Message);
            CloseSocket(ref socket);
            Connecting = false;
        }
    }

    /// <summary>Hang up and stop announcing or listening.</summary>
    public static void Close()
    {
        CloseSocket(ref socket);
        CloseSocket(ref listener);
        CloseSocket(ref beacon);
        CloseBluetooth();
        Connected = Connecting = false;
        CurrentRole = Role.None;
        inboxLen = readPos = 0;
        Hosts.Clear();
    }

    static void CloseBluetooth()
    {
        if (viaBluetooth || bluetoothHosting || bluetoothJoining) BluetoothLink.Close();
        viaBluetooth = bluetoothHosting = bluetoothJoining = false;
    }

    static void CloseSocket(ref Socket s)
    {
        if (s == null) return;
        try { s.Close(); } catch (Exception) { }
        s = null;
    }

    static void Drop(string why)
    {
        if (!Connected && !Connecting) return;
        Log("line dropped: " + why);
        Problem = CurrentRole == Role.Host ? "Druhý hráč se odpojil" : "Spojení s hostitelem se přerušilo";
        CloseSocket(ref socket);
        CloseBluetooth();
        Connected = Connecting = false;
        inboxLen = readPos = 0;
    }

    // ------------------------------------------------------------------ every frame

    public static void Tick()
    {
        float now = Time.realtimeSinceStartup;

        // host: somebody knocking?
        if (listener != null && !Connected)
        {
            try
            {
                if (listener.Poll(0, SelectMode.SelectRead))
                {
                    socket = listener.Accept();
                    Established();
                    Log("the other player joined from " + socket.RemoteEndPoint);
                    CloseSocket(ref listener);      // a game is for two: nobody else is let in,
                    CloseSocket(ref beacon);        // and it is no longer announced
                    CloseBluetooth();
                }
            }
            catch (Exception e) { Log("accept failed: " + e.Message); }
        }

        // host: or has a paired phone come in over Bluetooth?
        if (bluetoothHosting && !Connected)
        {
            int state = BluetoothLink.State;
            if (state == BluetoothLink.Connected)
            {
                bluetoothHosting = false;
                viaBluetooth = true;
                MarkConnected();
                Log("the other player joined over Bluetooth: " + BluetoothLink.Peer);
                CloseSocket(ref listener);
                CloseSocket(ref beacon);
            }
            else if (state == BluetoothLink.Failed)
            {
                Log("Bluetooth hosting failed: " + BluetoothLink.Problem);     // Wi-Fi is still waiting
                bluetoothHosting = false;
            }
        }

        // host: shout that a game is waiting; joiner: listen for the shouting
        if (beacon != null)
        {
            if (CurrentRole == Role.Host)
            {
                if (!Connected && now >= nextBeacon)
                {
                    nextBeacon = now + 1f;
                    SendBeacon();
                }
            }
            else ReceiveBeacons(now);
        }

        // joiner: has the call gone through?
        if (Connecting && socket != null)
        {
            try
            {
                if (socket.Poll(0, SelectMode.SelectError)) Fail("Na této adrese žádná hra nečeká");
                else if (socket.Poll(0, SelectMode.SelectWrite))
                {
                    int err = (int)socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Error);
                    if (err != 0) Fail("Na této adrese žádná hra nečeká");
                    else
                    {
                        Connecting = false;
                        Established();
                        Log("joined " + socket.RemoteEndPoint);
                    }
                }
                else if (now - connectStarted > 6f) Fail("Hostitel neodpovídá");
            }
            catch (Exception e) { Fail("Připojení se nepovedlo (" + e.Message + ")"); }
        }

        // joiner over Bluetooth: has that call gone through? (it can take a good ten seconds)
        if (Connecting && bluetoothJoining)
        {
            int state = BluetoothLink.State;
            if (state == BluetoothLink.Connected)
            {
                bluetoothJoining = false;
                viaBluetooth = true;
                MarkConnected();
                Log("joined over Bluetooth: " + BluetoothLink.Peer);
            }
            else if (state == BluetoothLink.Failed) Fail("Přes Bluetooth se nepodařilo připojit (čeká druhý telefon na hráče?)");
            else if (now - connectStarted > 20f) Fail("Druhý telefon přes Bluetooth neodpovídá");
        }

        // anything arrived? has the other side gone?
        if (Connected && viaBluetooth)
        {
            if (BluetoothLink.State != BluetoothLink.Connected) { Drop("Bluetooth: " + BluetoothLink.Problem); return; }
            byte[] data = BluetoothLink.Receive();
            if (data != null)
            {
                if (readPos > 0 && inboxLen + data.Length > inbox.Length) Compact();
                if (inboxLen + data.Length > inbox.Length) { Drop("inbox overflow"); return; }
                Buffer.BlockCopy(data, 0, inbox, inboxLen, data.Length);
                inboxLen += data.Length;
            }
        }
        else if (Connected && socket != null)
        {
            try
            {
                if (socket.Poll(0, SelectMode.SelectRead) && socket.Available == 0) { Drop("closed by the other side"); return; }
                while (socket.Available > 0)
                {
                    if (readPos > 0 && inboxLen + chunk.Length > inbox.Length) Compact();
                    int room = Mathf.Min(chunk.Length, inbox.Length - inboxLen);
                    if (room <= 0) { Drop("inbox overflow"); return; }
                    int got = socket.Receive(chunk, 0, room, SocketFlags.None);
                    if (got <= 0) { Drop("closed by the other side"); return; }
                    Buffer.BlockCopy(chunk, 0, inbox, inboxLen, got);
                    inboxLen += got;
                }
            }
            catch (Exception e) { Drop(e.Message); }
        }
    }

    static void Established()
    {
        socket.Blocking = true;         // reads are only made when data is waiting; sends are tiny
        socket.NoDelay = true;
        socket.SendTimeout = 1500;
        viaBluetooth = false;
        MarkConnected();
    }

    static void MarkConnected()
    {
        Connected = true;
        Connecting = false;
        Problem = "";
        inboxLen = readPos = 0;
    }

    static void Fail(string why)
    {
        Log("join failed: " + why);
        Problem = why;
        CloseSocket(ref socket);
        CloseBluetooth();
        Connecting = false;
        Connected = false;
    }

    static void Compact()
    {
        Buffer.BlockCopy(inbox, readPos, inbox, 0, inboxLen - readPos);
        inboxLen -= readPos;
        readPos = 0;
    }

    // ------------------------------------------------------------------ messages

    /// <summary>Send one message: a type byte and whatever <paramref name="write"/> puts after it.</summary>
    public static void Send(byte type, Action<BinaryWriter> write = null)
    {
        if (!Connected || (socket == null && !viaBluetooth)) return;
        try
        {
            outBuffer.SetLength(0);
            outWriter.Write((ushort)0);     // length, filled in below
            outWriter.Write(type);
            write?.Invoke(outWriter);
            outWriter.Flush();
            int length = (int)outBuffer.Length - 2;
            if (length > ushort.MaxValue) { Log("message too long: " + length); return; }
            byte[] raw = outBuffer.GetBuffer();
            raw[0] = (byte)(length & 0xff);
            raw[1] = (byte)(length >> 8);
            int sent = 0, total = (int)outBuffer.Length;
            if (viaBluetooth)
            {
                if (!BluetoothLink.Send(raw, total)) Drop("Bluetooth send failed");
                return;
            }
            while (sent < total) sent += socket.Send(raw, sent, total - sent, SocketFlags.None);
        }
        catch (Exception e) { Drop("send: " + e.Message); }
    }

    /// <summary>
    /// The next complete message that has arrived, if any. The reader is only
    /// valid until the next call.
    /// </summary>
    public static bool Receive(out byte type, out BinaryReader reader)
    {
        type = 0;
        reader = null;
        if (inboxLen - readPos < 3) return false;
        int length = inbox[readPos] | (inbox[readPos + 1] << 8);
        if (length < 1) { Drop("bad message"); return false; }
        if (inboxLen - readPos < 2 + length) return false;      // the rest is still on its way
        type = inbox[readPos + 2];
        reader = new BinaryReader(new MemoryStream(inbox, readPos + 3, length - 1, false), Encoding.UTF8);
        readPos += 2 + length;
        if (readPos == inboxLen) inboxLen = readPos = 0;
        return true;
    }

    // ------------------------------------------------------------------ finding each other

    static void SendBeacon()
    {
        byte[] data = Encoding.UTF8.GetBytes(Magic + "|" + SystemInfo.deviceModel);
        string mine = LocalAddress();
        var targets = new List<IPAddress> { IPAddress.Broadcast };
        int dot = mine.LastIndexOf('.');
        if (dot > 0 && IPAddress.TryParse(mine.Substring(0, dot) + ".255", out var subnet)) targets.Add(subnet);
        foreach (var t in targets)
        {
            try { beacon.SendTo(data, new IPEndPoint(t, BeaconPort)); }
            catch (Exception) { }       // some networks refuse one of the two: the other still gets through
        }
    }

    static void ReceiveBeacons(float now)
    {
        try
        {
            while (beacon.Available > 0)
            {
                EndPoint from = new IPEndPoint(IPAddress.Any, 0);
                int got = beacon.ReceiveFrom(chunk, ref from);
                string text = Encoding.UTF8.GetString(chunk, 0, got);
                if (!text.StartsWith(Magic + "|")) continue;
                string address = ((IPEndPoint)from).Address.ToString();
                var host = Hosts.Find(h => h.address == address);
                if (host == null)
                {
                    host = new Found { address = address };
                    Hosts.Add(host);
                    Log("found a host at " + address);
                }
                host.name = text.Substring(Magic.Length + 1);
                host.seen = now;
            }
        }
        catch (Exception) { }
        Hosts.RemoveAll(h => now - h.seen > 4f);
    }

    static string localAddress;
    static float localAddressAt = -100f;

    /// <summary>This device's address on the local network ("" if it has none). Looked up at most every few seconds.</summary>
    public static string LocalAddress()
    {
        float now = Time.realtimeSinceStartup;
        if (localAddress != null && now - localAddressAt < 5f) return localAddress;
        localAddressAt = now;
        localAddress = FindLocalAddress();
        return localAddress;
    }

    static string FindLocalAddress()
    {
        // Ask the system which address it would use to reach the outside. Connecting a UDP
        // socket sends nothing; it only picks the route. This works the same everywhere,
        // including on phones, where listing network interfaces is restricted.
        try
        {
            using (var probe = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                probe.Connect(new IPEndPoint(IPAddress.Parse("192.0.2.1"), 9));
                string a = ((IPEndPoint)probe.LocalEndPoint).Address.ToString();
                if (a.StartsWith("192.168.") || Application.isMobilePlatform) return a == "0.0.0.0" ? "" : a;
            }
        }
        catch (Exception) { }
        if (Application.isMobilePlatform) return "";

        // A computer may route "the outside" through a VPN: look for the home network by name.
        string best = "";
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                string n = ni.Name.ToLowerInvariant();
                if (n.StartsWith("tun") || n.StartsWith("docker") || n.StartsWith("veth") || n.StartsWith("br-") || n.StartsWith("rmnet")) continue;
                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    string a = ua.Address.ToString();
                    if (a.StartsWith("192.168.")) return a;                         // home Wi-Fi: the usual case
                    if (best == "" && (a.StartsWith("10.") || a.StartsWith("172."))) best = a;
                }
            }
        }
        catch (Exception) { }
        return best;
    }

    /// <summary>
    /// The short code a host shows and a joiner types: the last number of the
    /// host's address. Both devices are on the same network, so the rest matches.
    /// </summary>
    public static string CodeOf(string address)
    {
        int dot = address.LastIndexOf('.');
        return dot >= 0 ? address.Substring(dot + 1) : "";
    }

    /// <summary>The address a typed code stands for, seen from this device ("" if it makes none).</summary>
    public static string AddressFor(string code)
    {
        if (code.Contains(".")) return IPAddress.TryParse(code, out _) ? code : "";
        string mine = LocalAddress();
        int dot = mine.LastIndexOf('.');
        if (dot < 0 || !int.TryParse(code, out int n) || n < 1 || n > 254) return "";
        return mine.Substring(0, dot + 1) + n;
    }
}

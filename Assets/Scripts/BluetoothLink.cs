using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bluetooth for the two-player mode, phone to phone. Only Android has it (the
/// work is done by a small Java class in Assets/Plugins/Android/BtLink.androidlib);
/// everywhere else <see cref="Supported"/> is false and nothing here does anything.
///
/// The two phones must already be paired in Android's Bluetooth settings: the
/// joining phone picks the hosting one from its list of paired devices, so no
/// scanning (and none of the permissions scanning needs) is involved. From
/// Android 12 on the game must be allowed to use "nearby devices" once.
///
/// Every call is wrapped: if Bluetooth is missing, switched off or refused, the
/// game simply offers Wi-Fi only.
/// </summary>
public static class BluetoothLink
{
    public const int Idle = 0, Listening = 1, Connecting = 2, Connected = 3, Failed = 4;
    const string ConnectPermission = "android.permission.BLUETOOTH_CONNECT";

    /// <summary>A phone paired with this one.</summary>
    public struct Device
    {
        public string address, name;
    }

    /// <summary>True where Bluetooth play exists at all (Android devices).</summary>
    public static bool Supported => Application.platform == RuntimePlatform.Android;

#if UNITY_ANDROID && !UNITY_EDITOR
    static AndroidJavaClass java;
    static int sdk = -1;
    static bool asked;

    static AndroidJavaClass Java
    {
        get
        {
            if (java == null) java = new AndroidJavaClass("com.prdivyzavod.btlink.BtLink");
            return java;
        }
    }

    static int Sdk
    {
        get
        {
            if (sdk < 0)
            {
                try
                {
                    using (var version = new AndroidJavaClass("android.os.Build$VERSION")) sdk = version.GetStatic<int>("SDK_INT");
                }
                catch (Exception) { sdk = 0; }
            }
            return sdk;
        }
    }

    /// <summary>True once the game may talk to paired devices (always, before Android 12).</summary>
    public static bool Allowed => Sdk < 31 || UnityEngine.Android.Permission.HasUserAuthorizedPermission(ConnectPermission);

    /// <summary>Asks for the permission once per run, if it is needed and missing. The answer shows up in <see cref="Allowed"/>.</summary>
    public static void AskPermission()
    {
        if (Allowed || asked) return;
        asked = true;
        try { UnityEngine.Android.Permission.RequestUserPermission(ConnectPermission); }
        catch (Exception e) { Debug.Log("[Bt] cannot ask for the permission: " + e.Message); }
    }

    /// <summary>True if Bluetooth is there, switched on and allowed.</summary>
    public static bool Ready
    {
        get
        {
            if (!Allowed) return false;
            try { return Java.CallStatic<bool>("ready"); }
            catch (Exception) { return false; }
        }
    }

    /// <summary>The name other phones see this one under.</summary>
    public static string LocalName
    {
        get
        {
            try { return Java.CallStatic<string>("localName") ?? ""; }
            catch (Exception) { return ""; }
        }
    }

    /// <summary>The phones, tablets and computers paired with this device.</summary>
    public static List<Device> Paired()
    {
        var list = new List<Device>();
        try
        {
            string all = Java.CallStatic<string>("paired") ?? "";
            foreach (string line in all.Split('\n'))
            {
                int bar = line.IndexOf('|');
                if (bar > 0) list.Add(new Device { address = line.Substring(0, bar), name = line.Substring(bar + 1) });
            }
        }
        catch (Exception e) { Debug.Log("[Bt] cannot list paired devices: " + e.Message); }
        return list;
    }

    public static int State
    {
        get
        {
            try { return Java.CallStatic<int>("state"); }
            catch (Exception) { return Failed; }
        }
    }

    public static string Problem
    {
        get
        {
            try { return Java.CallStatic<string>("problem") ?? ""; }
            catch (Exception e) { return e.Message; }
        }
    }

    public static string Peer
    {
        get
        {
            try { return Java.CallStatic<string>("peer") ?? ""; }
            catch (Exception) { return ""; }
        }
    }

    /// <summary>Wait for a paired phone to join.</summary>
    public static bool Host()
    {
        try { Java.CallStatic("host"); return true; }
        catch (Exception e) { Debug.Log("[Bt] cannot host: " + e.Message); return false; }
    }

    /// <summary>Join the game hosted by the paired phone with this address.</summary>
    public static bool Join(string address)
    {
        try { Java.CallStatic("join", address); return true; }
        catch (Exception e) { Debug.Log("[Bt] cannot join: " + e.Message); return false; }
    }

    public static void Close()
    {
        if (java == null) return;
        try { Java.CallStatic("close"); }
        catch (Exception) { }
    }

    static sbyte[] outBuffer = new sbyte[1024];

    /// <summary>Send the first <paramref name="count"/> bytes. False if the line is down.</summary>
    public static bool Send(byte[] data, int count)
    {
        try
        {
            if (outBuffer.Length < count) outBuffer = new sbyte[Mathf.NextPowerOfTwo(count)];
            Buffer.BlockCopy(data, 0, outBuffer, 0, count);
            return Java.CallStatic<bool>("send", outBuffer, count);
        }
        catch (Exception e) { Debug.Log("[Bt] send failed: " + e.Message); return false; }
    }

    /// <summary>Whatever has arrived since the last call (null if nothing).</summary>
    public static byte[] Receive()
    {
        try
        {
            sbyte[] got = Java.CallStatic<sbyte[]>("receive");
            if (got == null || got.Length == 0) return null;
            var bytes = new byte[got.Length];
            Buffer.BlockCopy(got, 0, bytes, 0, got.Length);
            return bytes;
        }
        catch (Exception e) { Debug.Log("[Bt] receive failed: " + e.Message); return null; }
    }
#else
    public static bool Allowed => false;
    public static void AskPermission() { }
    public static bool Ready => false;
    public static string LocalName => "";
    public static List<Device> Paired() => new List<Device>();
    public static int State => Idle;
    public static string Problem => "";
    public static string Peer => "";
    public static bool Host() => false;
    public static bool Join(string address) => false;
    public static void Close() { }
    public static bool Send(byte[] data, int count) => false;
    public static byte[] Receive() => null;
#endif
}

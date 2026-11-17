using System;
using System.Collections;
using System.IO;
using UnityEngine;

/// <summary>
/// Dev aid for checking a standalone build without a screen: start the player with
///   PrdivyZavod -batchmode -captureShot /path/shot.png
/// and it plays for a few seconds, writes one frame (with the HUD) to that file and quits.
/// Does nothing unless that argument is present.
/// </summary>
public class DevCapture : MonoBehaviour
{
    string path;

    public static void StartIfRequested()
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-captureShot")
                new GameObject("DevCapture").AddComponent<DevCapture>().path = args[i + 1];
    }

    IEnumerator Start()
    {
        // ("-captureDelay SECONDS" to wait longer, e.g. for the other player to join)
        yield return new WaitForSecondsRealtime(Session.FlagValue("-captureDelay", 4f));
        var cam = Camera.main;
        if (cam == null) { Debug.LogError("DevCapture: no camera"); Application.Quit(1); yield break; }

        // an overlay canvas only exists on a real screen; hang the HUD (or the menu) in front of the camera for the picture
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 0.3f;
        }
        yield return null;

        const int w = 1600, h = 900;
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
        cam.targetTexture = rt;
        if (Hud.Instance != null) Hud.Instance.Refresh();
        Canvas.ForceUpdateCanvases();
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        cam.targetTexture = null;
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Debug.Log($"DevCapture wrote {path} ({SystemInfo.graphicsDeviceType}, {QualitySettings.names[QualitySettings.GetQualityLevel()]}, {FartAudio.RecordedClips} recorded fart clips)");
        Application.Quit(0);
    }
}

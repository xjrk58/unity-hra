using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Dev tool: renders a few of the SYNTHESISED fart sounds (the fallback used when the
/// recordings in Resources/Audio/Farts are missing) to .wav files so they can be
/// auditioned or analysed outside Unity.
///
///   Tools ▸ Prdivý závod ▸ Export fart sounds (to ./FartSounds)
///   or headless: Unity -batchmode -projectPath . -executeMethod AudioDump.Run -audioDir DIR -quit
/// </summary>
public static class AudioDump
{
    [MenuItem("Tools/Prdivý závod/Export synthesised fart sounds")]
    public static void Run()
    {
        string dir = "FartSounds";
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-audioDir") dir = args[i + 1];
        Directory.CreateDirectory(dir);

        var rng = new System.Random(4711);
        for (int i = 0; i < 4; i++) Write(Path.Combine(dir, $"short{i}.wav"), FartAudio.Synthesize(FartAudio.ShortSpec(rng), rng));
        for (int i = 0; i < 3; i++) Write(Path.Combine(dir, $"long{i}.wav"), FartAudio.Synthesize(FartAudio.LongSpec(rng), rng));
        for (int i = 0; i < 3; i++) Write(Path.Combine(dir, $"squeak{i}.wav"), FartAudio.Synthesize(FartAudio.SqueakSpec(rng), rng));

        // the fart monster's own sounds
        Write(Path.Combine(dir, "monster_drone.wav"), MonsterAudio.RenderDrone());
        Write(Path.Combine(dir, "monster_heartbeat.wav"), MonsterAudio.RenderHeartbeat());
        Write(Path.Combine(dir, "monster_sting.wav"), MonsterAudio.RenderSting());
        Write(Path.Combine(dir, "monster_roar.wav"), MonsterAudio.RenderRoar());
        Write(Path.Combine(dir, "monster_shriek.wav"), MonsterAudio.RenderShriek());
        Debug.Log("[Setup] fart sounds written to " + Path.GetFullPath(dir));
    }

    static void Write(string path, float[] samples)
    {
        using (var w = new BinaryWriter(File.Create(path)))
        {
            int bytes = samples.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + bytes);
            w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' }); w.Write(16);
            w.Write((short)1); w.Write((short)1);
            w.Write(FartAudio.SampleRate); w.Write(FartAudio.SampleRate * 2);
            w.Write((short)2); w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(bytes);
            foreach (float s in samples) w.Write((short)Mathf.RoundToInt(Mathf.Clamp(s, -1f, 1f) * 32767f));
        }
    }
}

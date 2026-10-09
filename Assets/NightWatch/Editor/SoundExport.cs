using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Night Watch > Export Sounds: saves every sound the game makes in code (SoundBank) as a .wav
// file, to listen to outside Unity. Goes to D:\NightWatchSounds (or a Sounds folder next to the
// project if there is no D: drive).
public static class SoundExport
{
    static string Folder => Directory.Exists("D:/") ? "D:/NightWatchSounds" : Path.GetFullPath("Sounds");

    [MenuItem("Night Watch/Export Sounds")]
    static void Export()
    {
        Directory.CreateDirectory(Folder);
        int count = 0;
        foreach (var p in typeof(SoundBank).GetProperties(BindingFlags.Public | BindingFlags.Static))
        {
            if (p.PropertyType != typeof(AudioClip)) continue;
            var clip = (AudioClip)p.GetValue(null);
            var data = new float[clip.samples * clip.channels];
            clip.GetData(data, 0);
            Write(Path.Combine(Folder, clip.name + ".wav"), data, clip.frequency, clip.channels);
            count++;
        }
        Debug.Log($"Night Watch: {count} sounds saved as .wav in {Folder}");
        EditorUtility.RevealInFinder(Folder);
    }

    // A plain 16-bit WAV file.
    static void Write(string path, float[] data, int rate, int channels)
    {
        using (var w = new BinaryWriter(File.Create(path)))
        {
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + data.Length * 2);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16);
            w.Write((short)1);
            w.Write((short)channels);
            w.Write(rate);
            w.Write(rate * channels * 2);
            w.Write((short)(channels * 2));
            w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            w.Write(data.Length * 2);
            foreach (var v in data) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(v * 32767f), -32768, 32767));
        }
    }
}

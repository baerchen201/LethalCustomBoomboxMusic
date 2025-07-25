using System.IO;
using UnityEngine;

namespace CustomBoomboxMusic;

public sealed class AudioFile
{
    public AudioFile(uint crc, AudioClip audioClip, string filePath)
    {
        Crc = crc;
        AudioClip = audioClip;
        FilePath = filePath;
    }

    public AudioFile(int vanillaId, AudioClip audioClip)
    {
        VanillaId = vanillaId;
        AudioClip = audioClip;
        FilePath = string.Format(AudioManager.VANILLA_AUDIO_CLIP_NAME, vanillaId + 1);
    }

    public int? VanillaId { get; }
    public uint? Crc { get; }
    public AudioClip AudioClip { get; }
    public string FilePath { get; }
    public string Name => Path.GetFileNameWithoutExtension(FilePath);

    public override string ToString()
    {
        return $"{{ VanillaId: {a(VanillaId)}, Crc: {a(Crc)}, AudioClip: {b(AudioClip)}, FilePath: {c(FilePath)}, Name: {c(Name)} }}";

        string a(object? e) => e == null ? "null" : e.ToString();
        string b(AudioClip e) => e == null || !e ? "null" : e.ToString();
        string c(string? e) =>
            e == null
                ? "null"
                : $"\"{e.Replace("\\", @"\\").Replace("\"", "\\\"").Replace("\n", "\\n")}\"";
    }
}

using UnityEngine;

namespace CustomBoomboxMusic;

public sealed class AudioFile(uint crc, AudioClip audioClip, string filePath)
{
    public uint Crc { get; } = crc;
    public AudioClip AudioClip { get; } = audioClip;
    public string FilePath { get; } = filePath;
}

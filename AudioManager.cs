using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using HarmonyLib;
using LethalModUtils;

namespace CustomBoomboxMusic;

public static class AudioManager
{
    private static List<AudioFile> audioClips = [];
    public static IReadOnlyList<AudioFile> AudioClips => audioClips;

    internal const string VANILLA_AUDIO_CLIP_NAME = "Boombox {0} (Lethal Company)";

    public static IReadOnlyList<AudioFile> VanillaAudioClips(BoomboxItem boombox) =>
        boombox.musicAudios.Select((clip, i) => new AudioFile(i, clip)).ToList();

    internal static void Reload()
    {
        audioClips.Do(f => f.AudioClip.UnloadAudioData());
        audioClips.Clear();
        FS.IterateDirectories(
            new DirectoryInfo(Paths.BepInExRootPath),
            ProcessFile,
            i =>
                i.Name.Equals(
                    CustomBoomboxMusic.DIRECTORY_NAME,
                    StringComparison.CurrentCultureIgnoreCase
                )
                    ? FS.ProcessFilter.All
                    : FS.ProcessFilter.DirectoriesOnly
        );
        audioClips = audioClips.OrderBy(f => f.Crc).ToList();
        CustomBoomboxMusic.BoomboxPlayPatch.rng = new();
    }

    private static void ProcessFile(FileInfo file)
    {
        CustomBoomboxMusic.Logger.LogDebug($">> ProcessFile(file: {file})");
        try
        {
            var audioClip = Audio.Load(
                new Uri(file.FullName),
                out var webRequest,
                CustomBoomboxMusic.Instance.LoadTimeOut
            );
            audioClips.Add(
                new AudioFile(
                    Crc32.Calculate(webRequest.downloadHandler.data),
                    audioClip,
                    file.FullName
                )
            );
        }
        catch (Exception e)
        {
            CustomBoomboxMusic.Logger.LogWarning($"Couldn't load file {file.FullName}: {e}");
        }
    }

    public static bool TryGetCrc(uint crc, out AudioFile audioClip) =>
        (audioClip = AudioClips.FirstOrDefault(i => i.Crc != null && i.Crc.Value == crc)!) != null;

    public static bool TryGetVanillaId(
        BoomboxItem boombox,
        int vanillaId,
        out AudioFile audioClip
    ) =>
        (
            audioClip = VanillaAudioClips(boombox)
                .FirstOrDefault(i => i.VanillaId != null && i.VanillaId.Value == vanillaId)!
        ) != null;
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using HarmonyLib;
using LethalModUtils;
using UnityEngine;
using UnityEngine.Networking;

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
        ProcessDirectory(Paths.BepInExRootPath);
        audioClips = audioClips.OrderBy(f => f.Crc).ToList();
        CustomBoomboxMusic.BoomboxPlayPatch.rng = new();
    }

    private static int ProcessDirectory(string path)
    {
        if (!Directory.Exists(path))
            return 0;
        CustomBoomboxMusic.Logger.LogDebug($">> ProcessDirectory({Path.GetFullPath(path)})");
        var i = Directory.GetDirectories(path).Sum(ProcessDirectory);
        if (
            Path.GetFileName(path)
                .Equals(
                    CustomBoomboxMusic.DIRECTORY_NAME,
                    StringComparison.CurrentCultureIgnoreCase
                )
        )
            i += Directory.GetFiles(path).Count(ProcessFile);
        return i;
    }

    private static bool ProcessFile(string path)
    {
        path = Path.GetFullPath(path);
        CustomBoomboxMusic.Logger.LogDebug($">> ProcessFile({path})");
        try
        {
            var audioClip = Audio.Load(
                new Uri(path),
                out var webRequest,
                CustomBoomboxMusic.Instance.LoadTimeOut
            );
            audioClips.Add(
                new AudioFile(Crc32.Calculate(webRequest.downloadHandler.data), audioClip, path)
            );
            return true;
        }
        catch (Exception e)
        {
            CustomBoomboxMusic.Logger.LogWarning($"Couldn't load file {path}: {e}");
            return false;
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

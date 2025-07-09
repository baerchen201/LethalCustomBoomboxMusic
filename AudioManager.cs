using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

namespace CustomBoomboxMusic;

public static class AudioManager
{
    private static List<AudioFile> audioClips = [];
    public static IReadOnlyCollection<AudioClip> AudioClips =>
        CustomBoomboxMusic.Instance.IncludeVanilla && vanilla != null
            ? audioClips.Select(f => f.AudioClip).Concat(vanilla).ToList()
            : audioClips.Select(f => f.AudioClip).ToList();

    internal static AudioClip[]? vanilla = null;

    internal static void Reload()
    {
        audioClips.Do(f => f.AudioClip.UnloadAudioData());
        audioClips.Clear();
        ProcessDirectory(Paths.BepInExRootPath);
        audioClips = audioClips.OrderBy(f => f.Crc).ToList();
    }

    private static int ProcessDirectory(string path)
    {
        if (!Directory.Exists(path))
            return 0;
        CustomBoomboxMusic.Logger.LogDebug($">> ProcessDirectory({Path.GetFullPath(path)})");
        int i = Directory.GetDirectories(path).Sum(ProcessDirectory);
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
        AudioType audioType = Path.GetExtension(path).ToLower() switch
        {
            ".ogg" => AudioType.OGGVORBIS,
            ".mp3" => AudioType.MPEG,
            ".wav" => AudioType.WAV,
            ".m4a" => AudioType.ACC,
            ".aiff" => AudioType.AIFF,
            _ => AudioType.UNKNOWN,
        };
        CustomBoomboxMusic.Logger.LogDebug(
            $">> ProcessFile({Path.GetFullPath(path)}) audioType:{audioType}"
        );
        if (audioType == AudioType.UNKNOWN)
            return false;

        var webRequest = UnityWebRequestMultimedia.GetAudioClip(path, audioType);
        ((DownloadHandlerAudioClip)webRequest.downloadHandler).streamAudio = !CustomBoomboxMusic
            .Instance
            .LoadIntoRAM;
        webRequest.SendWebRequest();
        while (!webRequest.isDone) { }

        if (webRequest.error != null)
        {
            CustomBoomboxMusic.Logger.LogError(
                $"Error loading {Path.GetFullPath(path)}: {webRequest.error}"
            );
            return false;
        }

        var audioClip = DownloadHandlerAudioClip.GetContent(webRequest);
        if (audioClip && audioClip.loadState == AudioDataLoadState.Loaded)
        {
            CustomBoomboxMusic.Logger.LogInfo($"Loaded {Path.GetFileName(path)}");
            audioClips.Add(
                new AudioFile(
                    Crc32.Calculate(webRequest.downloadHandler.data),
                    audioClip,
                    Path.GetFullPath(path)
                )
            );
            return true;
        }

        CustomBoomboxMusic.Logger.LogWarning(
            $"Error loading {Path.GetFullPath(path)}: {audioClip.loadState}"
        );
        return false;
    }
}

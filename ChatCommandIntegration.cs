using System;
using System.Collections.Generic;
using System.Linq;
using ChatCommandAPI;
using HarmonyLib;
using UnityEngine;

namespace CustomBoomboxMusic;

internal static class ChatCommandIntegration
{
    internal static void Init()
    {
        _ = new BoomboxCommand();
    }
}

public class BoomboxCommand : Command
{
    public override string Name => "Boombox";
    public override string Description => "Various commands for the CustomBoomboxMusic mod";
    public override string[] Syntax => ["reload", "version", "list"];

    public override bool Invoke(string[] args, Dictionary<string, string> kwargs, out string? error)
    {
        error = null;

        var command = args.Length > 0 ? args[0] : null;
        switch (command)
        {
            case "reload" or "r":
                ChatCommandAPI.ChatCommandAPI.Print("Reloading...");
                AudioManager.Reload();
                ChatCommandAPI.ChatCommandAPI.Print(
                    $"Done reloading, found {a(AudioManager.AudioClips.Count)}"
                );
                break;
            case "version" or "v" or null:
                ChatCommandAPI.ChatCommandAPI.Print(
                    $"{MyPluginInfo.PLUGIN_NAME} v{MyPluginInfo.PLUGIN_VERSION}\n{a(AudioManager.AudioClips.Count)} loaded"
                );
                break;
            case "list" or "l":
                var clips = AudioManager.AudioClips.OrderBy(i => i.Name).ToList();
                var count = clips.Count;
                ChatCommandAPI.ChatCommandAPI.Print(
                    count == 0
                        ? $"{a(count)} loaded"
                        : $"{a(count)} loaded:\n> {clips.Join(i => i.Name, "\n> ")}"
                );
                CustomBoomboxMusic.Logger.LogInfo($"Listing loaded tracks ({count}):");
                foreach (var clip in clips)
                    CustomBoomboxMusic.Logger.LogInfo(
                        $"> {clip.Name} - {clip.FilePath} (CRC32: {clip.Crc})"
                    );
                break;
            case "play":
                error = "Invalid arguments";
                if (args.Length < 2)
                    return false;
                error = "You need to be holding a boombox";
                GrabbableObject? boomboxObject;
                if (
                    (
                        boomboxObject = GameNetworkManager
                            .Instance
                            ?.localPlayerController
                            ?.currentlyHeldObjectServer
                    ) == null
                )
                    return false;
                if (!boomboxObject.gameObject.TryGetComponent<BoomboxItem>(out var boombox))
                    return false;

                error = "Track could not be found";
                return Play(args[1..].Join(null, " "), boombox);
            default:
                ChatCommandAPI.ChatCommandAPI.PrintError(
                    "Invalid subcommand, use /help for usage information"
                );
                break;
        }

        return true;

        string a(int count) => count == 0 ? "No tracks" : $"{count} track{b(count)}";
        string b(int count) => count == 1 ? string.Empty : "s";
    }

    private static bool Play(string identifier, BoomboxItem boombox)
    {
        var files = AudioManager.AudioClips;
        if (CustomBoomboxMusic.Instance.IncludeVanilla || files.Count == 0)
            files = files.Concat(AudioManager.VanillaAudioClips(boombox)).ToList();
        List<AudioFile?> clips = [];
        if (uint.TryParse(identifier, out var crc))
            if (AudioManager.TryGetCrc(crc, out var clip))
                clips.Add(clip);
        if (clips.Count == 0)
            clips.AddRange(
                files.Where(i =>
                    string.Equals(i.Name, identifier, StringComparison.CurrentCultureIgnoreCase)
                )
            );
        if (clips.Count == 0)
            clips.AddRange(
                files.Where(i =>
                    i.Name.StartsWith(identifier, StringComparison.CurrentCultureIgnoreCase)
                )
            );
        if (clips.Count == 0)
            return false;

        foreach (var clip in clips)
        {
            if (clip == null)
                continue;
            if (ModNetworkBehaviour.Instance != null)
            {
                if (clip.VanillaId != null)
                    ModNetworkBehaviour.Instance.StartPlayingVanillaMusicServerRpc(
                        boombox.NetworkObject,
                        clip.VanillaId.Value
                    );
                else if (clip.Crc != null)
                    ModNetworkBehaviour.Instance.StartPlayingMusicServerRpc(
                        boombox.NetworkObject,
                        clip.Crc.Value,
                        clip.Name
                    );
                else
                {
                    CustomBoomboxMusic.Logger.LogWarning(
                        $"AudioFile doesn't have CRC nor VanillaID: {clip}"
                    );
                    continue;
                }

                return true;
            }

            if (!boombox.isBeingUsed)
                boombox.ActivateItemServerRpc(true, true);
            boombox.boomboxAudio.clip = clip.AudioClip;
            boombox.boomboxAudio.pitch = 1f;
            boombox.boomboxAudio.Play();
            boombox.isBeingUsed = boombox.isPlayingMusic = true;
            a(clip);
        }

        return false;

        void a(AudioFile audioFile)
        {
            if (
                !GameNetworkManager.Instance
                || !GameNetworkManager.Instance.localPlayerController
                || GameNetworkManager.Instance.localPlayerController.isPlayerDead
                || !GameNetworkManager.Instance.localPlayerController.isPlayerControlled
            )
                return;
            CustomBoomboxMusic.Logger.LogDebug($">> Play({audioFile}, {boombox})");
            if (
                Vector3.Distance(
                    boombox.boomboxAudio.transform.position,
                    GameNetworkManager.Instance.localPlayerController.transform.position
                ) <= boombox.boomboxAudio.maxDistance
            )
                CustomBoomboxMusic.AnnouncePlaying(audioFile);
        }
    }
}

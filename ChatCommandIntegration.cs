using System.Collections.Generic;
using System.Linq;
using ChatCommandAPI;
using HarmonyLib;

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
}

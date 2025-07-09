using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace CustomBoomboxMusic;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("com.sigurd.csync", BepInDependency.DependencyFlags.SoftDependency)]
public class CustomBoomboxMusic : BaseUnityPlugin
{
    public static CustomBoomboxMusic Instance { get; private set; } = null!;
    internal static new ManualLogSource Logger { get; private set; } = null!;
    internal static Harmony? Harmony { get; set; }

    public const string DIRECTORY_NAME = "CustomBoomboxMusic";

    internal ConfigEntry<bool> loadIntoRAM = null!;
    internal bool LoadIntoRAM => loadIntoRAM.Value;
    internal ConfigEntry<bool> displayNowPlaying = null!;
    internal bool DisplayNowPlaying => displayNowPlaying.Value;
    internal ConfigEntry<bool> includeVanilla = null!;
    internal bool IncludeVanilla => ClientSide ? includeVanilla.Value : CSync.IncludeVanilla;

    internal static bool ClientSide => !Chainloader.PluginInfos.ContainsKey("com.sigurd.csync");

    private void Awake()
    {
        Logger = base.Logger;
        Instance = this;

        loadIntoRAM = Config.Bind(
            "General",
            "LoadIntoRAM",
            true,
            "Loads music into RAM, recommended if you use an HDD, not recommended if you have 8GB of RAM or less"
        );
        displayNowPlaying = Config.Bind(
            "General",
            "DisplayNowPlaying",
            true,
            "Whether to display a popup about which song is currently playing"
        );
        if (!ClientSide)
            CSync.Initialize(this);
        else
            includeVanilla = Config.Bind(
                "General",
                "IncludeVanilla",
                true,
                "Includes vanilla music (forced true if no custom music is present)"
            );

        AudioManager.Reload();

        Harmony ??= new Harmony(MyPluginInfo.PLUGIN_GUID);
        Logger.LogDebug("Patching...");
        Harmony.PatchAll();
        Logger.LogDebug("Finished patching!");

        Logger.LogInfo($"{MyPluginInfo.PLUGIN_GUID} v{MyPluginInfo.PLUGIN_VERSION} has loaded!");
    }

    [HarmonyPatch(typeof(BoomboxItem), nameof(BoomboxItem.ItemActivate))]
    internal class BoomboxUsePatch
    {
        // ReSharper disable once UnusedMember.Local
        private static void Prefix(ref BoomboxItem __instance)
        {
            AudioManager.vanilla ??= (AudioClip[])__instance.musicAudios.Clone();
            __instance.musicAudios = AudioManager.AudioClips.ToArray();
        }
    }

    [HarmonyPatch(typeof(BoomboxItem), nameof(BoomboxItem.StartMusic))]
    internal class BoomboxPlayPatch
    {
        // ReSharper disable once UnusedMember.Local
        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions
        ) =>
            new CodeMatcher(instructions)
                .MatchForward(false, new CodeMatch(OpCodes.Ldelem_Ref))
                .Insert(
                    new CodeInstruction(OpCodes.Dup),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(
                        OpCodes.Call,
                        AccessTools.Method(typeof(BoomboxPlayPatch), nameof(a))
                    )
                )
                .InstructionEnumeration();

        internal static void a(int songId, BoomboxItem boombox)
        {
            if (
                !GameNetworkManager.Instance
                || !GameNetworkManager.Instance.localPlayerController
                || GameNetworkManager.Instance.localPlayerController.isPlayerDead
                || !GameNetworkManager.Instance.localPlayerController.isPlayerControlled
            )
                return;
            Logger.LogDebug($">> BoomboxPlayPatch(#{songId}, {boombox})");
            if (
                Vector3.Distance(
                    boombox.boomboxAudio.transform.position,
                    GameNetworkManager.Instance.localPlayerController.transform.position
                ) <= boombox.boomboxAudio.maxDistance
            )
                AnnouncePlaying(songId);
        }
    }

    internal static void AnnouncePlaying(int songId)
    {
        if (!Instance.DisplayNowPlaying)
            return;
        var audioFiles = AudioManager.AudioFiles;
        if (songId >= audioFiles.Count || songId < 0)
            return;
        var name = Path.GetFileNameWithoutExtension(audioFiles[songId].FilePath);
        Logger.LogInfo($"Now playing: {name}");
        HUDManager.Instance.DisplayTip("Now playing:", $"{name}");
    }
}

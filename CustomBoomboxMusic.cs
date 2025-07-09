using System.Linq;
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
}

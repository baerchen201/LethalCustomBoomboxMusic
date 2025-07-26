using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace CustomBoomboxMusic;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class CustomBoomboxMusic : BaseUnityPlugin
{
    public static CustomBoomboxMusic Instance { get; private set; } = null!;
    internal static new ManualLogSource Logger { get; private set; } = null!;
    internal static Harmony? Harmony { get; set; }

    public const string DIRECTORY_NAME = "CustomBoomboxMusic";

    private ConfigEntry<bool> loadIntoRAM = null!;
    public bool LoadIntoRAM => loadIntoRAM.Value;

    private ConfigEntry<bool> displayNowPlaying = null!;
    public bool DisplayNowPlaying => displayNowPlaying.Value;

    private ConfigEntry<bool> includeVanilla = null!;
    public bool IncludeVanilla => includeVanilla.Value;

    private ConfigEntry<bool> clientSide = null!;
    public bool ClientSide { get; private set; }

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
        includeVanilla = Config.Bind(
            "General",
            "IncludeVanilla",
            true,
            "Includes vanilla music (forced true if no custom music is present)"
        );
        clientSide = Config.Bind(
            "General",
            "ClientSide",
            true,
            "Enables or disables custom networking to more accurately sync which song is currently playing"
        );
        ClientSide = clientSide.Value;
        clientSide.SettingChanged += (_, _) =>
            Logger.LogWarning("ClientSide requires a restart of the game to apply");
        Logger.LogInfo($"Client-side mode {(ClientSide ? "enabled" : "disabled")}");

        AudioManager.Reload();

        ModNetworkBehaviour.InitializeRPCS_ModNetworkBehaviour();

        Harmony ??= new Harmony(MyPluginInfo.PLUGIN_GUID);
        Logger.LogDebug("Patching...");
        Harmony.PatchAll();
        Logger.LogDebug("Finished patching!");

        Logger.LogInfo($"{MyPluginInfo.PLUGIN_GUID} v{MyPluginInfo.PLUGIN_VERSION} has loaded!");
    }

    [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.Start))]
    internal class StartPatch
    {
        // ReSharper disable once UnusedMember.Local
        private static void Postfix()
        {
            if (Instance.ClientSide || networkPrefab != null)
                return;

            networkPrefab = new GameObject(
                $"{MyPluginInfo.PLUGIN_GUID}-{nameof(ModNetworkBehaviour)}"
            );
            networkPrefab.AddComponent<NetworkObject>();
            networkPrefab.AddComponent<ModNetworkBehaviour>();

            DontDestroyOnLoad(networkPrefab);
            networkPrefab.hideFlags = HideFlags.HideAndDontSave;

            NetworkManager.Singleton.AddNetworkPrefab(networkPrefab);
            Logger.LogDebug($"Registered network prefab {a(networkPrefab)}");
        }
    }

    private static string a(GameObject? e) => e == null ? "null" : e.ToString();

    private static GameObject networkPrefab = null!;

    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.Awake))]
    internal class InitPatch
    {
        // ReSharper disable once UnusedMember.Local
        private static void Postfix()
        {
            Logger.LogDebug(
                $">> InitPatch() ClientSide:{Instance.ClientSide} IsHost:{NetworkManager.Singleton.IsHost} IsServer:{NetworkManager.Singleton.IsServer} networkPrefab:{a(networkPrefab)}"
            );
            if (
                Instance.ClientSide
                || (!NetworkManager.Singleton.IsHost && !NetworkManager.Singleton.IsServer)
                || networkPrefab == null
            )
            {
                Logger.LogDebug("<< InitPatch false");
                return;
            }

            var networkHandlerHost = Instantiate(networkPrefab, Vector3.zero, Quaternion.identity);
            networkHandlerHost.GetComponent<NetworkObject>().Spawn();
            Logger.LogDebug("<< InitPatch true");
        }
    }

    [HarmonyPatch(typeof(BoomboxItem), nameof(BoomboxItem.ItemActivate))]
    internal class BoomboxUsePatch
    {
        // ReSharper disable once UnusedMember.Local
        private static void Prefix(ref BoomboxItem __instance) =>
            AudioManager.vanillaAudioClips ??= (AudioClip[])__instance.musicAudios.Clone();
    }

    [HarmonyPatch(typeof(BoomboxItem), nameof(BoomboxItem.StartMusic))]
    internal class BoomboxPlayPatch
    {
        // ReSharper disable once UnusedMember.Local
        private static bool Prefix(
            ref BoomboxItem __instance,
            ref bool startMusic,
            ref bool pitchDown
        )
        {
            if (!startMusic)
                return true;
            if (!Instance.ClientSide && !__instance.IsOwner)
                return false;

            var clips = AudioManager.AudioClips;
            if (Instance.IncludeVanilla)
                clips = clips.Concat(AudioManager.VanillaAudioClips).ToList();
            var clip = clips[__instance.musicRandomizer.Next(clips.Count)];

            if (ModNetworkBehaviour.Instance != null)
                if (clip.VanillaId != null)
                    ModNetworkBehaviour.Instance.StartPlayingVanillaMusicServerRpc(
                        __instance.NetworkObject,
                        clip.VanillaId.Value
                    );
                else if (clip.Crc != null)
                    ModNetworkBehaviour.Instance.StartPlayingMusicServerRpc(
                        __instance.NetworkObject,
                        clip.Crc.Value,
                        clip.Name
                    );
                else
                    Logger.LogWarning($"AudioFile doesn't have CRC nor VanillaID: {clip}");
            else
            {
                __instance.boomboxAudio.clip = clip.AudioClip;
                __instance.boomboxAudio.pitch = 1f;
                __instance.boomboxAudio.Play();
                __instance.isBeingUsed = __instance.isPlayingMusic = startMusic;
                a(clip, __instance);
            }

            return false;
        }

        private static void a(AudioFile audioFile, BoomboxItem boombox)
        {
            if (
                !GameNetworkManager.Instance
                || !GameNetworkManager.Instance.localPlayerController
                || GameNetworkManager.Instance.localPlayerController.isPlayerDead
                || !GameNetworkManager.Instance.localPlayerController.isPlayerControlled
            )
                return;
            Logger.LogDebug($">> BoomboxPlayPatch({audioFile}, {boombox})");
            if (
                Vector3.Distance(
                    boombox.boomboxAudio.transform.position,
                    GameNetworkManager.Instance.localPlayerController.transform.position
                ) <= boombox.boomboxAudio.maxDistance
            )
                AnnouncePlaying(audioFile);
        }
    }

    internal static void AnnouncePlaying(AudioFile audioFile)
    {
        if (!Instance.DisplayNowPlaying)
            return;
        var name = audioFile.Name;
        Logger.LogInfo($"Now playing: {name}");
        HUDManager.Instance.DisplayTip("Now playing:", $"{name}");
    }

    internal static void AnnounceMissing(string missingName)
    {
        Logger.LogWarning($"Missing audio for {missingName}");
        HUDManager.Instance.DisplayTip(
            "Missing audio:",
            $"{missingName} could not be played",
            true
        );
    }
}

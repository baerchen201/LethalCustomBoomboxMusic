using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using EasyTextEffects.Editor.MyBoxCopy.Extensions;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace CustomBoomboxMusic;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("baer1.LethalModUtils")]
[BepInDependency("baer1.ChatCommandAPI", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(SPECTATE_ENEMIES, BepInDependency.DependencyFlags.SoftDependency)]
public class CustomBoomboxMusic : BaseUnityPlugin
{
    public static CustomBoomboxMusic Instance { get; private set; } = null!;
    internal static new ManualLogSource Logger { get; private set; } = null!;
    internal static Harmony? Harmony { get; set; }

    public const string DIRECTORY_NAME = "CustomBoomboxMusic";

    private ConfigEntry<float> loadTimeOut = null!;
    public TimeSpan LoadTimeOut => TimeSpan.FromSeconds(loadTimeOut.Value);

    private ConfigEntry<bool> displayNowPlaying = null!;
    public bool DisplayNowPlaying => displayNowPlaying.Value;

    private ConfigEntry<bool> includeVanilla = null!;
    public bool IncludeVanilla => includeVanilla.Value;

    private ConfigEntry<bool> newRNG = null!;
    public bool NewRNG => newRNG.Value;

    private ConfigEntry<bool> clientSide = null!;
    public bool ClientSide { get; private set; }

    internal const string SPECTATE_ENEMIES = "SpectateEnemy";

    private void Awake()
    {
        Logger = base.Logger;
        Instance = this;

        loadTimeOut = Config.Bind(
            "General",
            "LoadTimeOut",
            10f,
            "Maximum amount of time to wait for an audio file to load. Increase this value if you have giant files or are using a slow drive"
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
        includeVanilla.SettingChanged += (_, _) => BoomboxPlayPatch.rng = new();
        newRNG = Config.Bind(
            "General",
            "NewRNG",
            true,
            "Enables an improved RNG which prevents repeats"
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

#if !SPECTATE_ENEMIES
#warning Missing SpectateEnemies DLL, compiling without SpectateEnemies support
        (
            (Action<string>)(
                Chainloader.PluginInfos.ContainsKey(SPECTATE_ENEMIES)
                    ? Logger.LogWarning
                    : Logger.LogDebug
            )
        ).Invoke(
            "This version of the mod was compiled without SpectateEnemies support.\n"
                + "  If you compiled this mod yourself, you can recompile it with the SpectateEnemies DLL.\n"
                + "  If you downloaded this mod from an official source, please report this error."
        );
#endif
        AudioManager.Reload();

        if (Chainloader.PluginInfos.ContainsKey("baer1.ChatCommandAPI"))
            ChatCommandIntegration.Init();

        Harmony ??= new Harmony(MyPluginInfo.PLUGIN_GUID);
        Logger.LogDebug("Patching...");
        Harmony.PatchAll();
        Logger.LogDebug("Finished patching!");

        Logger.LogInfo($"{MyPluginInfo.PLUGIN_GUID} v{MyPluginInfo.PLUGIN_VERSION} has loaded!");
    }

    [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.Start))]
    internal static class StartPatch
    {
        // ReSharper disable once UnusedMember.Local
        private static void Postfix()
        {
            Logger.LogDebug(
                $">> StartPatch() ClientSide:{Instance.ClientSide} networkPrefab:{a(networkPrefab)}"
            );
            if (Instance.ClientSide || networkPrefab != null)
                return;

            var bundle = AssetBundle.LoadFromFile(
                Path.Combine(
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty,
                    "modnetworkmanager"
                )
            );

            networkPrefab = bundle.LoadAsset<GameObject>("ModNetworkManager");
            networkPrefab.name = $"{MyPluginInfo.PLUGIN_GUID}-ModNetworkManager";
            networkPrefab.AddComponent<ModNetworkBehaviour>();

            NetworkManager.Singleton.AddNetworkPrefab(networkPrefab);
            Logger.LogDebug($"   Registered network prefab {a(networkPrefab)}");
        }
    }

    private static string a(GameObject? e) => e == null ? "null" : e.ToString();

    private static GameObject networkPrefab = null!;

    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.Awake))]
    internal static class InitPatch
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
            networkHandlerHost.GetComponent<NetworkObject>().Spawn(true);
            Logger.LogDebug("<< InitPatch true");
        }
    }

    [HarmonyPatch(typeof(BoomboxItem), nameof(BoomboxItem.StartMusic))]
    internal static class BoomboxPlayPatch
    {
        private static IReadOnlyList<AudioFile> GetClips(BoomboxItem __instance)
        {
            var clips = AudioManager.AudioClips;
            if (Instance.IncludeVanilla || clips.Count == 0)
                clips = clips.Concat(AudioManager.VanillaAudioClips(__instance)).ToList();
            return clips;
        }

        internal static ConditionalWeakTable<BoomboxItem, IEnumerator<AudioFile>> rng = new();

        private static IEnumerator<AudioFile> RNG(BoomboxItem __instance)
        {
            var clips = GetClips(__instance);
            Logger.LogDebug($">> RNG({__instance}) clips.Count: {clips.Count}");
            var clipIds = Enumerable.Range(0, clips.Count).ToList();
            while (clipIds.Count > 0)
            {
                var i = __instance.musicRandomizer.Next(clipIds.Count);
                var id = clipIds[i];
                clipIds.RemoveAt(i);
                Logger.LogDebug(
                    $"Requested new RNG Value for {__instance}: {id} ({clipIds.Count} remaining)"
                );
                yield return clips[id];
            }
        }

        private static AudioFile OldRNG(BoomboxItem __instance)
        {
            var clips = GetClips(__instance);
            Logger.LogDebug($">> OldRNG({__instance}) clips.Count: {clips.Count}");
            return clips[__instance.musicRandomizer.Next(clips.Count)];
        }

        private static AudioFile GetNextClip(BoomboxItem __instance)
        {
            Logger.LogDebug($">> GetNextClip({__instance})");
            if (!Instance.NewRNG)
                return OldRNG(__instance);
            if (!rng.TryGetValue(__instance, out var enumerator))
            {
                Logger.LogDebug("   Failed to obtain enumerator, creating new...");
                create();
            }

            var clip = enumerator.Current!;
            Logger.LogDebug($"   Got clip: {clip}");
            if (!enumerator.MoveNext())
            {
                Logger.LogDebug("   Failed to advance RNG, creating new...");
                create();
            }

            return clip;

            void create()
            {
                rng.AddOrUpdate(__instance, enumerator = RNG(__instance));
                enumerator.MoveNext();
            }
        }

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

            var clip = GetNextClip(__instance);

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
            Logger.LogDebug(
                $">> BoomboxPlayPatch({audioFile}, {boombox}) IsOwner:{boombox.IsOwner}"
            );
            if (
                GameNetworkManager.Instance?.localPlayerController == null
                || (
                    GameNetworkManager.Instance.localPlayerController.isPlayerDead
                    && !GameNetworkManager.Instance.localPlayerController.hasBegunSpectating
                )
            )
                return;
            if (
                Vector3.Distance(
                    boombox.boomboxAudio.transform.position,
                    GameNetworkManager.Instance.localPlayerController.isPlayerDead
                        ? GameNetworkManager
                            .Instance
                            .localPlayerController
                            .spectatedPlayerScript
                            .transform
                            .position
                        : GameNetworkManager.Instance.localPlayerController.transform.position
                ) <= boombox.boomboxAudio.maxDistance
                || boombox.IsOwner
                || boombox.OwnerClientId
                    == GameNetworkManager
                        .Instance
                        .localPlayerController
                        .spectatedPlayerScript
                        .actualClientId
            )
                AnnouncePlaying(audioFile);
        }
    }

    internal static void AnnouncePlaying(AudioFile audioFile)
    {
        var name = audioFile.Name;
        Logger.LogInfo($"Now playing: {name}");
        if (!Instance.DisplayNowPlaying)
            return;
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

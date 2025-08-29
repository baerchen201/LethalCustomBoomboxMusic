using Unity.Netcode;
using UnityEngine;

namespace CustomBoomboxMusic;

public class ModNetworkBehaviour : NetworkBehaviour
{
    public static ModNetworkBehaviour? Instance { get; private set; }

    public override void OnNetworkSpawn()
    {
        CustomBoomboxMusic.Logger.LogDebug(
            $">> OnNetworkSpawn() Instance:{Instance?.ToString() ?? "null"}"
        );
        if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
            Instance?.gameObject.GetComponent<NetworkObject>()?.Despawn();
        Instance = this;

        base.OnNetworkSpawn();
    }

    public override void OnNetworkDespawn()
    {
        CustomBoomboxMusic.Logger.LogDebug(
            $">> OnNetworkDespawn() Instance:{Instance?.ToString() ?? "null"} ==this:{Instance == this}"
        );
        if (Instance == this)
            Instance = null;
        base.OnNetworkDespawn();
    }

    private const uint START_PLAYING_MUSIC_SERVER_RPC_ID = 2501615839U;

    private static void Play(BoomboxItem boombox, AudioFile clip)
    {
        CustomBoomboxMusic.Logger.LogDebug($">> Play({boombox}, {clip}) IsOwner:{boombox.IsOwner}");
        boombox.boomboxAudio.clip = clip.AudioClip;
        boombox.boomboxAudio.pitch = 1f;
        boombox.boomboxAudio.Play();
        boombox.isBeingUsed = boombox.isPlayingMusic = true;

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
            CustomBoomboxMusic.AnnouncePlaying(clip);
    }

    private static void PlayFallback(BoomboxItem boombox, string missingName)
    {
        CustomBoomboxMusic.Logger.LogDebug(
            $">> PlayFallback({boombox}, {missingName}) IsOwner:{boombox.IsOwner}"
        );

        boombox.boomboxAudio.clip = boombox.musicAudios[
            boombox.musicRandomizer.Next(boombox.musicAudios.Length)
        ];
        boombox.boomboxAudio.pitch = 1f;
        boombox.boomboxAudio.Play();
        boombox.isBeingUsed = boombox.isPlayingMusic = true;

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
            CustomBoomboxMusic.AnnounceMissing(missingName);
    }

    [ServerRpc(RequireOwnership = false)]
    public void StartPlayingMusicServerRpc(
        NetworkObjectReference boomboxObjectReference,
        uint crc,
        string? clipName = null
    )
    {
        var networkManager = NetworkManager;
        if (networkManager == null || !networkManager.IsListening)
            return;
        if (
            __rpc_exec_stage != __RpcExecStage.Server
            && (networkManager.IsClient || networkManager.IsHost)
        )
        {
            ServerRpcParams serverRpcParams = default;
            var writer = __beginSendServerRpc(
                START_PLAYING_MUSIC_SERVER_RPC_ID,
                serverRpcParams,
                RpcDelivery.Reliable
            );
            writer.WriteValueSafe(boomboxObjectReference);
            BytePacker.WriteValueBitPacked(writer, crc);
            var isMissingNameNull = clipName == null;
            writer.WriteValueSafe(isMissingNameNull);
            if (!isMissingNameNull)
                writer.WriteValueSafe(clipName);
            __endSendServerRpc(
                ref writer,
                START_PLAYING_MUSIC_SERVER_RPC_ID,
                serverRpcParams,
                RpcDelivery.Reliable
            );
        }
        if (
            __rpc_exec_stage != __RpcExecStage.Server
            || networkManager is { IsServer: false, IsHost: false }
        )
            return;

        CustomBoomboxMusic.Logger.LogDebug(
            $">> StartPlayingMusicServerRpc({boomboxObjectReference}, {crc})"
        );
        if (boomboxObjectReference.TryGet(out _))
            StartPlayingMusicClientRpc(boomboxObjectReference, crc, clipName);
        else
            CustomBoomboxMusic.Logger.LogWarning(
                "[StartPlayingMusicServerRpc] Boombox object could not be found, dropping request"
            );
    }

    private const uint START_PLAYING_MUSIC_CLIENT_RPC_ID = 636642922U;

    [ClientRpc]
    public void StartPlayingMusicClientRpc(
        NetworkObjectReference boomboxObjectReference,
        uint crc,
        string? clipName = null
    )
    {
        var networkManager = NetworkManager;
        if (networkManager == null || !networkManager.IsListening)
            return;
        if (
            __rpc_exec_stage != __RpcExecStage.Client
            && (networkManager.IsServer || networkManager.IsHost)
        )
        {
            ClientRpcParams clientRpcParams = default;
            var writer = __beginSendClientRpc(
                START_PLAYING_MUSIC_CLIENT_RPC_ID,
                clientRpcParams,
                RpcDelivery.Reliable
            );
            writer.WriteValueSafe(boomboxObjectReference);
            BytePacker.WriteValueBitPacked(writer, crc);
            var isMissingNameNull = clipName == null;
            writer.WriteValueSafe(isMissingNameNull);
            if (!isMissingNameNull)
                writer.WriteValueSafe(clipName);
            __endSendClientRpc(
                ref writer,
                START_PLAYING_MUSIC_CLIENT_RPC_ID,
                clientRpcParams,
                RpcDelivery.Reliable
            );
        }
        if (
            __rpc_exec_stage != __RpcExecStage.Client
            || networkManager is { IsClient: false, IsHost: false }
        )
            return;

        CustomBoomboxMusic.Logger.LogDebug(
            $">> StartPlayingMusicClientRpc({boomboxObjectReference}, {crc}, {clipName})"
        );
        if (!boomboxObjectReference.TryGet(out var boomboxObject))
        {
            CustomBoomboxMusic.Logger.LogWarning(
                "[StartPlayingMusicClientRpc] Boombox object could not be found, dropping request"
            );
            return;
        }
        CustomBoomboxMusic.Logger.LogDebug($"   {boomboxObject}");

        if (!boomboxObject.TryGetComponent<BoomboxItem>(out var boombox))
        {
            CustomBoomboxMusic.Logger.LogWarning(
                "[StartPlayingMusicClientRpc] Boombox component could not be found, dropping request"
            );
            return;
        }
        CustomBoomboxMusic.Logger.LogDebug($"   {boombox}");

        if (!AudioManager.TryGetCrc(crc, out var clip))
        {
            CustomBoomboxMusic.Logger.LogWarning(
                $"Couldn't find AudioClip with crc32 {crc}, playing fallback"
            );
            PlayFallback(boombox, $"{clipName} (CRC32: {crc})");
            return;
        }
        CustomBoomboxMusic.Logger.LogDebug($"   {clip}");

        Play(boombox, clip);
    }

    private const uint START_PLAYING_VANILLA_MUSIC_SERVER_RPC_ID = 2501615849U;

    [ServerRpc(RequireOwnership = false)]
    public void StartPlayingVanillaMusicServerRpc(
        NetworkObjectReference boomboxObjectReference,
        int vanillaId
    )
    {
        var networkManager = NetworkManager;
        if (networkManager == null || !networkManager.IsListening)
            return;
        if (
            __rpc_exec_stage != __RpcExecStage.Server
            && (networkManager.IsClient || networkManager.IsHost)
        )
        {
            ServerRpcParams serverRpcParams = default;
            var writer = __beginSendServerRpc(
                START_PLAYING_VANILLA_MUSIC_SERVER_RPC_ID,
                serverRpcParams,
                RpcDelivery.Reliable
            );
            writer.WriteValueSafe(boomboxObjectReference);
            BytePacker.WriteValueBitPacked(writer, vanillaId);
            __endSendServerRpc(
                ref writer,
                START_PLAYING_VANILLA_MUSIC_SERVER_RPC_ID,
                serverRpcParams,
                RpcDelivery.Reliable
            );
        }
        if (
            __rpc_exec_stage != __RpcExecStage.Server
            || networkManager is { IsServer: false, IsHost: false }
        )
            return;

        CustomBoomboxMusic.Logger.LogDebug(
            $">> StartPlayingVanillaMusicServerRpc({boomboxObjectReference}, {vanillaId})"
        );
        if (boomboxObjectReference.TryGet(out _))
            StartPlayingVanillaMusicClientRpc(boomboxObjectReference, vanillaId);
        else
            CustomBoomboxMusic.Logger.LogWarning(
                "[StartPlayingVanillaMusicServerRpc] Boombox object could not be found, dropping request"
            );
    }

    private const uint START_PLAYING_VANILLA_MUSIC_CLIENT_RPC_ID = 636642923U;

    [ClientRpc]
    public void StartPlayingVanillaMusicClientRpc(
        NetworkObjectReference boomboxObjectReference,
        int vanillaId
    )
    {
        var networkManager = NetworkManager;
        if (networkManager == null || !networkManager.IsListening)
            return;
        if (
            __rpc_exec_stage != __RpcExecStage.Client
            && (networkManager.IsServer || networkManager.IsHost)
        )
        {
            ClientRpcParams clientRpcParams = default;
            var writer = __beginSendClientRpc(
                START_PLAYING_VANILLA_MUSIC_CLIENT_RPC_ID,
                clientRpcParams,
                RpcDelivery.Reliable
            );
            writer.WriteValueSafe(boomboxObjectReference);
            BytePacker.WriteValueBitPacked(writer, vanillaId);
            __endSendClientRpc(
                ref writer,
                START_PLAYING_VANILLA_MUSIC_CLIENT_RPC_ID,
                clientRpcParams,
                RpcDelivery.Reliable
            );
        }
        if (
            __rpc_exec_stage != __RpcExecStage.Client
            || networkManager is { IsClient: false, IsHost: false }
        )
            return;

        CustomBoomboxMusic.Logger.LogDebug(
            $">> StartPlayingVanillaMusicClientRpc({boomboxObjectReference}, {vanillaId})"
        );
        if (!boomboxObjectReference.TryGet(out var boomboxObject))
        {
            CustomBoomboxMusic.Logger.LogWarning(
                "[StartPlayingVanillaMusicClientRpc] Boombox object could not be found, dropping request"
            );
            return;
        }
        CustomBoomboxMusic.Logger.LogDebug($"   {boomboxObject}");

        if (!boomboxObject.TryGetComponent<BoomboxItem>(out var boombox))
        {
            CustomBoomboxMusic.Logger.LogWarning(
                "[StartPlayingMusicClientRpc] Boombox component could not be found, dropping request"
            );
            return;
        }
        CustomBoomboxMusic.Logger.LogDebug($"   {boombox}");

        if (!AudioManager.TryGetVanillaId(boombox, vanillaId, out var clip))
        {
            CustomBoomboxMusic.Logger.LogWarning(
                $"Couldn't find AudioClip with vanillaId {vanillaId}, playing fallback"
            );
            PlayFallback(
                boombox,
                string.Format(AudioManager.VANILLA_AUDIO_CLIP_NAME, vanillaId + 1)
            );
            return;
        }
        CustomBoomboxMusic.Logger.LogDebug($"   {clip}");

        Play(boombox, clip);
    }

    internal static void InitializeRPCS_ModNetworkBehaviour()
    {
        NetworkManager.__rpc_func_table.Add(
            START_PLAYING_MUSIC_SERVER_RPC_ID,
            __rpc_handler_StartPlayingMusicServerRpc
        );
        NetworkManager.__rpc_func_table.Add(
            START_PLAYING_MUSIC_CLIENT_RPC_ID,
            __rpc_handler_StartPlayingMusicClientRpc
        );
        NetworkManager.__rpc_func_table.Add(
            START_PLAYING_VANILLA_MUSIC_SERVER_RPC_ID,
            __rpc_handler_StartPlayingVanillaMusicServerRpc
        );
        NetworkManager.__rpc_func_table.Add(
            START_PLAYING_VANILLA_MUSIC_CLIENT_RPC_ID,
            __rpc_handler_StartPlayingVanillaMusicClientRpc
        );
    }

    private static void __rpc_handler_StartPlayingMusicServerRpc(
        NetworkBehaviour target,
        FastBufferReader reader,
        __RpcParams rpcParams
    )
    {
        var networkManager = target.NetworkManager;
        if (networkManager == null || !networkManager.IsListening)
            return;
        reader.ReadValueSafe(out NetworkObjectReference boomboxObjectReference);
        ByteUnpacker.ReadValueBitPacked(reader, out uint crc);
        reader.ReadValueSafe(out bool isMissingNameNull);
        string? clipName = null;
        if (!isMissingNameNull)
            reader.ReadValueSafe(out clipName);
        ((ModNetworkBehaviour)target).__rpc_exec_stage = __RpcExecStage.Server;
        ((ModNetworkBehaviour)target).StartPlayingMusicServerRpc(
            boomboxObjectReference,
            crc,
            clipName
        );
        ((ModNetworkBehaviour)target).__rpc_exec_stage = __RpcExecStage.None;
    }

    private static void __rpc_handler_StartPlayingMusicClientRpc(
        NetworkBehaviour target,
        FastBufferReader reader,
        __RpcParams rpcParams
    )
    {
        var networkManager = target.NetworkManager;
        if (networkManager == null || !networkManager.IsListening)
            return;
        reader.ReadValueSafe(out NetworkObjectReference boomboxObjectReference);
        ByteUnpacker.ReadValueBitPacked(reader, out uint crc);
        reader.ReadValueSafe(out bool isMissingNameNull);
        string? clipName = null;
        if (!isMissingNameNull)
            reader.ReadValueSafe(out clipName);
        ((ModNetworkBehaviour)target).__rpc_exec_stage = __RpcExecStage.Client;
        ((ModNetworkBehaviour)target).StartPlayingMusicClientRpc(
            boomboxObjectReference,
            crc,
            clipName
        );
        ((ModNetworkBehaviour)target).__rpc_exec_stage = __RpcExecStage.None;
    }

    private static void __rpc_handler_StartPlayingVanillaMusicServerRpc(
        NetworkBehaviour target,
        FastBufferReader reader,
        __RpcParams rpcParams
    )
    {
        var networkManager = target.NetworkManager;
        if (networkManager == null || !networkManager.IsListening)
            return;
        reader.ReadValueSafe(out NetworkObjectReference boomboxObjectReference);
        ByteUnpacker.ReadValueBitPacked(reader, out int vanillaId);
        ((ModNetworkBehaviour)target).__rpc_exec_stage = __RpcExecStage.Server;
        ((ModNetworkBehaviour)target).StartPlayingVanillaMusicServerRpc(
            boomboxObjectReference,
            vanillaId
        );
        ((ModNetworkBehaviour)target).__rpc_exec_stage = __RpcExecStage.None;
    }

    private static void __rpc_handler_StartPlayingVanillaMusicClientRpc(
        NetworkBehaviour target,
        FastBufferReader reader,
        __RpcParams rpcParams
    )
    {
        var networkManager = target.NetworkManager;
        if (networkManager == null || !networkManager.IsListening)
            return;
        reader.ReadValueSafe(out NetworkObjectReference boomboxObjectReference);
        ByteUnpacker.ReadValueBitPacked(reader, out int vanillaId);
        ((ModNetworkBehaviour)target).__rpc_exec_stage = __RpcExecStage.Client;
        ((ModNetworkBehaviour)target).StartPlayingVanillaMusicClientRpc(
            boomboxObjectReference,
            vanillaId
        );
        ((ModNetworkBehaviour)target).__rpc_exec_stage = __RpcExecStage.None;
    }

    protected override string __getTypeName() => GetType().Name;
}

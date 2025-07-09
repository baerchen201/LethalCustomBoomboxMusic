using CSync.Extensions;
using CSync.Lib;

namespace CustomBoomboxMusic;

internal static class CSync
{
    internal static bool IncludeVanilla =>
        CSync_instance.Instance == null || CSync_instance.Instance.includeVanilla.Value;

    internal static void Initialize(CustomBoomboxMusic plugin)
    {
        CSync_instance.Instance = new CSync_instance
        {
            includeVanilla = plugin.Config.BindSyncedEntry(
                "General",
                "IncludeVanilla",
                true,
                "Includes vanilla music (synced to host, forced true if no custom music is present)"
            ),
        };
    }
}

internal class CSync_instance
{
    internal static CSync_instance? Instance { get; set; }
    internal SyncedEntry<bool> includeVanilla = null!;
}

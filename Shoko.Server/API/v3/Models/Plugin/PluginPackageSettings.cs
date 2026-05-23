
#nullable enable
namespace Shoko.Server.API.v3.Models.Plugin;

/// <summary>
///   Settings for the plugin package manager's APIv3 REST API.
/// </summary>
public class PluginPackageSettings
{
    /// <summary>
    ///   Whether automatic repository syncing is enabled.
    /// </summary>
    public bool IsAutoSyncEnabled { get; set; }

    /// <summary>
    ///   Whether automatic plugin upgrades are enabled.
    /// </summary>
    public bool IsAutoUpgradeEnabled { get; set; }

    /// <summary>
    ///   Default time before a repository's packages are considered stale.
    /// </summary>
    public System.TimeSpan DefaultRepositoryStaleTime { get; set; }

    /// <summary>
    ///   Time to retain old plugin versions before auto-cleanup.
    /// </summary>
    public System.TimeSpan InactivePluginVersionRetention { get; set; }
}

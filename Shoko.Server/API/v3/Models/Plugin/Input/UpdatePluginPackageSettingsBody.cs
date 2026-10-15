
using System;

namespace Shoko.Server.API.v3.Models.Plugin.Input;

/// <summary>
///   Settings for the plugin package manager's APIv3 REST API.
/// </summary>
public class UpdatePluginPackageSettingsBody
{
    /// <summary>
    ///   Whether automatic plugin upgrades are enabled.
    /// </summary>
    public bool? IsAutoUpgradeEnabled { get; init; }

    /// <summary>
    ///   Whether plugins are upgraded while the server starts.
    /// </summary>
    public bool? IsUpgradeOnStartEnabled { get; init; }

    /// <summary>
    ///   Default time before a repository's packages are considered stale.
    /// </summary>
    public TimeSpan? DefaultRepositoryStaleTime { get; init; }

    /// <summary>
    ///   How old a repository's packages may be when the server starts
    ///   before the upgrades on start fetch them again.
    /// </summary>
    public TimeSpan? RepositoryStaleTimeOnStart { get; init; }

    /// <summary>
    ///   Time to retain old plugin versions before auto-cleanup.
    /// </summary>
    public TimeSpan? InactivePluginVersionRetention { get; init; }
}

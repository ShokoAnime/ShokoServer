
using System;
using System.Collections.Generic;
using Shoko.Abstractions.Core;

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
    ///   Whether the automatic upgrades leave the plugins the server ships
    ///   alone, so they only change with the server.
    /// </summary>
    public bool? SkipBundledPluginUpgrades { get; init; }

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

    /// <summary>
    ///   The release channel the upgrades follow: <c>Auto</c> (the server's
    ///   own), <c>Stable</c> or <c>Dev</c>.
    /// </summary>
    public ReleaseChannel? Channel { get; init; }

    /// <summary>
    ///   The channels of the plugins to change, by plugin ID; <c>Auto</c>
    ///   makes a plugin follow <see cref="Channel"/> again. Plugins left out
    ///   keep theirs.
    /// </summary>
    public Dictionary<Guid, ReleaseChannel>? PluginChannels { get; init; }
}

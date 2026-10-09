using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.ScheduledActions;

namespace Shoko.Server.Actions;

/// <summary>
///   Mark the inactive plugin versions superseded longer ago than the
///   retention in the plugin settings for removal on the next start.
/// </summary>
/// <param name="packageManager">The plugin package manager.</param>
public sealed class PurgeInactivePluginVersionsAction(IPluginPackageManager packageManager) : IScheduledAction
{
    public string Name => "Purge Inactive Plugin Versions";

    public string? Description => "Removes, on the next start, the inactive plugin versions whose newer version was installed longer ago than the inactive plugin version retention allows, counted from that install rather than from the restart that activated it. Pending updates, pinned versions and versions with a .keep file stay, and the plugins' configuration and data are kept.";

    public ActionCategory Category => ActionCategory.Destructive;

    public IReadOnlyList<ActionTrigger> DefaultTriggers => [ActionTrigger.Every(TimeSpan.FromHours(24))];

    public Task Execute(IProgress<decimal> progress, CancellationToken token)
    {
        packageManager.PurgeInactivePluginVersions();
        return Task.CompletedTask;
    }
}

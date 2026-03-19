using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Abstractions.Plugin;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;

#pragma warning disable CS8618
#nullable enable
namespace Shoko.Server.Scheduling.Jobs.Actions;

/// <summary>
///   Job that checks for plugin updates and optionally performs upgrades.
/// </summary>
[DatabaseRequired]
[NetworkRequired]
[JobKeyMember("CheckPluginUpdates")]
[JobKeyGroup(JobKeyGroup.Actions)]
[DisallowConcurrentExecution]
public class CheckPluginUpdatesJob : BaseJob
{
    private readonly IPluginPackageManager _pluginPackageManager;

    /// <summary>
    ///   Force sync even if not stale. If null, checks the configured schedule.
    /// </summary>
    public virtual bool? ForceSync { get; set; }

    /// <summary>
    ///   Whether to perform upgrades on enabled plugins. If null, uses settings default.
    /// </summary>
    public virtual bool? PerformUpgrade { get; set; }

    public override string TypeName => "Check Plugin Updates";

    public override string Title => "Checking for Plugin Updates";

    public override Dictionary<string, object> Details => new()
    {
        { "ForceSync", ForceSync?.ToString() ?? "Auto" },
        { "PerformUpgrade", PerformUpgrade?.ToString() ?? "Auto" },
    };

    public override async Task Process()
    {
        _logger.LogInformation("Processing CheckPluginUpdatesJob: ForceSync={ForceSync}, PerformUpgrade={PerformUpgrade}", ForceSync, PerformUpgrade);
        await _pluginPackageManager.CheckForUpdates(ForceSync, PerformUpgrade).ConfigureAwait(false);
    }

    public CheckPluginUpdatesJob(IPluginPackageManager pluginPackageManager)
    {
        _pluginPackageManager = pluginPackageManager;
    }

    protected CheckPluginUpdatesJob() { }
}

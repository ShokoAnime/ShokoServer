using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Abstractions.Utilities;
using Shoko.Server.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Refresh everything linked from the plugin sources, for every anime, and
///   the stored collections.
/// </summary>
/// <remarks>
///   TMDB has update actions of its own.
///   Only queues the refreshes, which run on their own; the progress covers
///   the queuing.
/// </remarks>
public sealed class RefreshAllLinkedMetadataAction(IMetadataProviderManager providerManager, IMetadataRefreshService refreshService) : IScheduledAction
{
    public string Name => "Refresh All Linked Metadata";

    public string? Description
        => "Has the plugin metadata providers refresh everything linked, for every anime, and every stored collection, "
            + "that was not refreshed within the last hour.";

    public ActionCategory Category => ActionCategory.Maintenance;

    public async Task Execute(IProgress<decimal> progress, CancellationToken token)
    {
        var sources = providerManager.MetadataProviders
            .Where(info => info.Enabled && !info.Source.IsCore)
            .Select(info => info.Source)
            .Distinct()
            .ToList();
        var stages = new StagedProgress(progress, Math.Max(sources.Count, 1));
        foreach (var source in sources)
        {
            await refreshService.RefreshAllLinked(source, false, null, null, stages, token).ConfigureAwait(false);
            stages.NextStage();
        }
    }
}

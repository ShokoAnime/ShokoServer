using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Abstractions.Utilities;

namespace Shoko.Server.Actions;

/// <summary>
///   Refresh everything linked from every source with an enabled provider,
///   for every anime, and the stored collections.
/// </summary>
/// <remarks>
///   Only queues the refreshes, which run on their own; the progress covers
///   the queuing. <see cref="RefreshLinkedMetadataAction"/> does it for one
///   source, one kind, or forced.
/// </remarks>
public sealed class RefreshAllLinkedMetadataAction(IMetadataProviderManager providerManager, IMetadataRefreshService refreshService) : IScheduledAction
{
    public string Name => "Refresh All Linked Metadata";

    public string? Description
        => "Has every metadata provider refresh everything linked, for every anime, and every stored collection, "
            + "that was not refreshed within the last hour.";

    public ActionCategory Category => ActionCategory.Maintenance;

    public async Task Execute(IProgress<decimal> progress, CancellationToken token)
    {
        var sources = providerManager.MetadataProviders
            .Where(info => info.Enabled)
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

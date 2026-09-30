using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.ScheduledActions;

namespace Shoko.Server.Actions;

/// <summary>
///   Refresh everything linked from the plugin sources, for every anime, and
///   the stored collections.
/// </summary>
/// <remarks>
///   TMDB has update actions of its own.
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
        foreach (var source in sources)
            await refreshService.RefreshAllLinked(source, cancellationToken: token).ConfigureAwait(false);
    }
}

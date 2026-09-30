using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.ScheduledActions;

namespace Shoko.Server.Actions;

/// <summary>
///   Search the plugin sources for every anime not linked on them yet.
/// </summary>
/// <remarks>
///   The searches are scheduled ones, so a source that does not auto-link and
///   an anime left alone are skipped. TMDB has a search action of its own.
/// </remarks>
public sealed class SearchForMetadataMatchesAction(IMetadataProviderManager providerManager, IMetadataRefreshService refreshService) : IScheduledAction
{
    public string Name => "Search for Metadata Matches";

    public string? Description => "Searches the plugin metadata sources for every anime not linked on them yet.";

    public ActionCategory Category => ActionCategory.Maintenance;

    public async Task Execute(IProgress<decimal> progress, CancellationToken token)
    {
        var sources = providerManager.MetadataProviders
            .Where(info => !info.Source.IsCore && info.AutoLink)
            .Select(info => info.Source)
            .Distinct()
            .ToList();
        foreach (var source in sources)
            await refreshService.AutoSearchAll(source, cancellationToken: token).ConfigureAwait(false);
    }
}

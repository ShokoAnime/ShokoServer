using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Server.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Search every metadata source that auto-links, TMDB included, for every
///   anime not linked on it yet.
/// </summary>
/// <remarks>
///   A source counts only while it has an enabled auto-linker with auto-linking
///   turned on, so a disabled TMDB is not searched. An unconfigured source and
///   an anime left alone are skipped.
/// </remarks>
/// <param name="providerManager">Lists the metadata providers.</param>
/// <param name="refreshService">Schedules the searches.</param>
public sealed class SearchForMetadataMatchesAction(IMetadataProviderManager providerManager, IMetadataRefreshService refreshService) : IScheduledAction
{
    public string Name => "Search for Metadata Matches";

    public string? Description => "Searches every metadata source that auto-links, TMDB included, for every anime not linked on it yet.";

    public ActionCategory Category => ActionCategory.Maintenance;

    public async Task Execute(IProgress<decimal> progress, CancellationToken token)
    {
        foreach (var autoLinker in providerManager.GetAutoLinkers())
            await refreshService.AutoSearchAll(autoLinker.Source, cancellationToken: token).ConfigureAwait(false);
    }
}

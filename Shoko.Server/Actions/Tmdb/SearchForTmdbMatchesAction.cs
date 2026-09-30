using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.ScheduledActions;

namespace Shoko.Server.Actions;

/// <summary>
///   Scan for TMDB matches for all AniDB anime that are not yet linked.
/// </summary>
public sealed class SearchForTmdbMatchesAction(IMetadataRefreshService refreshService) : IScheduledAction
{
    public string Name => "Search for TMDB Matches";

    public string? Description => "Scan for TMDB show and movie matches for all unlinked AniDB anime.";

    public ActionCategory Category => ActionCategory.TMDB;

    public Task Execute(IProgress<decimal> progress, CancellationToken token)
        => refreshService.AutoSearchAll(MetadataSource.TMDB, cancellationToken: token);
}

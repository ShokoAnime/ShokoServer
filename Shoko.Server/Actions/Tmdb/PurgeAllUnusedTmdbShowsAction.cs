using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Server.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Purge all TMDB shows that are not linked to any AniDB anime.
/// </summary>
/// <remarks>
///   Only queues the purges, which run on their own; the progress covers
///   the queuing.
/// </remarks>
public sealed class PurgeAllUnusedTmdbShowsAction(IMetadataPurgeService purgeService) : IScheduledAction
{
    public string Name => "Purge Unused TMDB Shows";

    public string? Description => "Remove all TMDB shows that are not linked to any AniDB anime.";

    public ActionCategory Category => ActionCategory.TMDB;

    public bool RequiresConfirmation => true;

    public string? ConfirmationMessage => "Are you sure you want to remove all unused TMDB shows from the database?";

    public Task Execute(IProgress<decimal> progress, CancellationToken token)
        => purgeService.PurgeUnused(MetadataSource.TMDB, null, MetadataEntityType.Series, progress, token);
}

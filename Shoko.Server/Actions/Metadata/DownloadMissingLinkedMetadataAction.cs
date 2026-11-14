using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Abstractions.Utilities;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Fetch what the links between AniDB and the other sources name but is not
///   stored: the series, film or episode on the other source, and the AniDB
///   anime of a series in the library.
/// </summary>
/// <remarks>
///   Only queues the refreshes, which run on their own; the progress covers
///   the queuing. A link's entry is fetched when the link is made, so this
///   catches a fetch that failed or a provider that was off then.
/// </remarks>
/// <param name="refreshService">Queues the other sources' refreshes.</param>
/// <param name="actionService">Queues the AniDB anime.</param>
/// <param name="crossReferences">The links.</param>
/// <param name="anidbAnimes">The stored AniDB anime.</param>
/// <param name="animeSeries">The series in the library.</param>
public sealed class DownloadMissingLinkedMetadataAction(
    IMetadataRefreshService refreshService,
    ActionService actionService,
    IMetadataCrossReferenceStore crossReferences,
    AniDB_AnimeRepository anidbAnimes,
    AnimeSeriesRepository animeSeries
) : IScheduledAction
{
    public string Name => "Download Missing Linked Metadata";

    public string? Description => "Fetch the series, films and episodes the links between AniDB and the other sources name but are not stored, and the AniDB anime of linked series in the library that are not stored.";

    public ActionCategory Category => ActionCategory.Maintenance;

    public TimeSpan? MinimumInterval => TimeSpan.FromHours(6);

    public bool ScheduleCountsManualRuns => true;

    public async Task Execute(IProgress<decimal> progress, CancellationToken token)
    {
        var stages = new StagedProgress(progress, 2);
        await ((MetadataRefreshService)refreshService).RefreshMissingLinked(stages, token).ConfigureAwait(false);
        stages.NextStage();

        var animeIDs = crossReferences.GetAllSeriesLinks().Select(link => link.AnidbAnimeID)
            .Concat(crossReferences.GetAllMovieLinks().Select(link => link.AnidbAnimeID))
            .Concat(crossReferences.GetAllEpisodeLinks().Select(link => link.AnidbAnimeID))
            .Distinct()
            .Where(animeID => anidbAnimes.GetByAnimeID(animeID) is null && animeSeries.GetByAnimeID(animeID) is not null)
            .ToList();
        var items = new ItemProgress(stages, animeIDs.Count);
        foreach (var animeID in animeIDs)
        {
            token.ThrowIfCancellationRequested();
            await actionService.QueueAniDBRefresh(animeID, false, false, false).ConfigureAwait(false);
            items.Increment();
        }

        stages.Complete();
    }
}

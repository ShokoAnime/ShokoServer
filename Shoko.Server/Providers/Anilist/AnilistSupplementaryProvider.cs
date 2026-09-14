using System.Threading.Tasks;
using Shoko.Abstractions.Metadata.Services;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.Anilist;
using Shoko.Server.Scheduling.Jobs.Anilist;
using Shoko.Server.Settings;

namespace Shoko.Server.Providers.Anilist;

/// <summary>
/// Supplementary metadata provider that schedules AniList anime updates
/// (auto-search for new series, and refresh for already-linked ones)
/// after AniDB data is confirmed. Mirrors <see cref="TMDB.TmdbSupplementaryProvider"/>.
/// </summary>
public class AnilistSupplementaryProvider(
    IQueueScheduler scheduler,
    ISettingsProvider settingsProvider,
    AnimeSeriesRepository seriesRepository,
    CrossRef_AniDB_Anilist_AnimeRepository crossRefRepository,
    AnilistLinkingService linkingService
) : ISupplementaryMetadataProvider
{
    /// <inheritdoc />
    public string Name => "AniList";

    /// <inheritdoc />
    public async Task ScheduleForAnime(int anidbAnimeID, bool isNew)
    {
        var settings = settingsProvider.GetSettings();
        var series = seriesRepository.GetByAnimeID(anidbAnimeID);

        // For newly-created series without an existing AniList link, trigger auto-search.
        if (series is not null && settings.Anilist.AutoLink && !series.IsAnilistAutoMatchingDisabled)
        {
            var existingLinks = crossRefRepository.GetByAnidbAnimeID(anidbAnimeID);
            if (existingLinks.Count == 0)
            {
                await scheduler.RunAfterCurrent<SearchAnilistForMatchJob>(c => c.AnimeID = anidbAnimeID);
                return;
            }
        }

        // For series that already have AniList links, refresh each anime.
        var xrefs = crossRefRepository.GetByAnidbAnimeID(anidbAnimeID);
        foreach (var xref in xrefs)
            await scheduler.RunAfterCurrent<UpdateAnilistAnimeJob>(job =>
            {
                job.AnilistAnimeID = xref.AnilistAnimeID;
                job.DownloadImages = true;
            });
    }

    /// <inheritdoc />
    public Task OnSeriesRemoved(int anidbAnimeID)
        => linkingService.RemoveAllAnimeLinksForAnidbAnime(anidbAnimeID);
}

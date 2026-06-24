#nullable enable
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata.Services;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Scheduling.Jobs.TMDB;
using Shoko.Server.Settings;

namespace Shoko.Server.Providers.TMDB;

/// <summary>
/// Supplementary metadata provider that schedules TMDB show updates
/// (auto-search for new series, and refresh for already-linked ones)
/// after AniDB data is confirmed.
/// </summary>
public class TmdbSupplementaryProvider(
    IQueueScheduler scheduler,
    ISettingsProvider settingsProvider,
    AnimeSeriesRepository seriesRepository,
    AniDB_AnimeRepository anidbAnimeRepository,
    CrossRef_AniDB_TMDB_ShowRepository crossRefRepository
) : ISupplementaryMetadataProvider
{
    /// <inheritdoc />
    public string Name => "TMDB";

    /// <inheritdoc />
    public async Task ScheduleForAnime(int anidbAnimeID, bool isNew)
    {
        var settings = settingsProvider.GetSettings();
        var series = seriesRepository.GetByAnimeID(anidbAnimeID);

        // For newly-created series without an existing TMDB link, trigger auto-search.
        if (series is not null && settings.TMDB.AutoLink && !series.IsTmdbAutoMatchingDisabled)
        {
            var existingLinks = crossRefRepository.GetByAnidbAnimeID(anidbAnimeID);
            if (existingLinks.Count == 0)
            {
                await scheduler.RunAfterCurrent<SearchTmdbJob>(c => c.AnimeID = anidbAnimeID);
                return;
            }
        }

        // For series that already have TMDB links, refresh each show.
        var xrefs = crossRefRepository.GetByAnidbAnimeID(anidbAnimeID);
        foreach (var xref in xrefs)
            await scheduler.RunAfterCurrent<UpdateTmdbShowJob>(job =>
            {
                job.TmdbShowID = xref.TmdbShowID;
                job.DownloadImages = true;
            });
    }
}

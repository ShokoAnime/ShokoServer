using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.Abstractions.Metadata.Services;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Settings;

namespace Shoko.Server.Services;

/// <summary>
/// Schedules primary AniDB metadata refreshes and delegates supplementary
/// metadata work after a release is saved, for any provider.
/// Extracted from <c>AnidbReleaseProvider.OnReleaseSaved</c> so the logic
/// runs for all providers, not just AniDB.
/// </summary>
public class AnimeMetadataOrchestrator(
    ILogger<AnimeMetadataOrchestrator> logger,
    IAnidbService anidbService,
    ISupplementaryMetadataService supplementaryMetadataService,
    AniDB_AnimeRepository anidbAnimeRepository,
    AniDB_AnimeUpdateRepository anidbAnimeUpdateRepository,
    AnimeSeriesRepository shokoSeriesRepository,
    IQueueScheduler scheduler,
    ISettingsProvider settingsProvider
)
{
    private IServerSettings Settings => settingsProvider.GetSettings();

    /// <summary>
    /// Schedules AniDB refresh and supplementary metadata for all anime IDs
    /// referenced in the supplied legacy cross-references.
    /// </summary>
    public async Task ScheduleForXrefs(IReadOnlyList<CrossRef_File_Episode> xrefs)
    {
        var animeIDs = xrefs
            .GroupBy(xref => xref.AnimeID)
            .ExceptBy([0], g => g.Key)
            .ToDictionary(
                g => g.Key,
                g =>
                    anidbAnimeRepository.GetByAnimeID(g.Key) is null ||
                    shokoSeriesRepository.GetByAnimeID(g.Key) is null ||
                    anidbAnimeUpdateRepository.GetByAnimeID(g.Key) is null
            );

        if (animeIDs.Count == 0)
            return;

        var refreshMethod = AnidbRefreshMethod.Default | AnidbRefreshMethod.CreateShokoSeries;
        if (Settings.AutoGroupSeries || Settings.AniDb.DownloadRelatedAnime)
            refreshMethod |= AnidbRefreshMethod.DownloadRelations;

        foreach (var (animeID, missingData) in animeIDs)
        {
            var update = anidbAnimeUpdateRepository.GetByAnimeID(animeID);
            var animeRecentlyUpdated = !missingData
                && update is not null
                && (DateTime.Now - update.UpdatedAt).TotalHours < Settings.AniDb.MinimumHoursToRedownloadAnimeInfo;

            if (missingData)
            {
                logger.LogInformation("Queuing immediate GET for AniDB_Anime: {AnimeID}", animeID);
                await anidbService.ScheduleRefreshOfAnimeByID(animeID, refreshMethod, prioritize: true);
            }
            else if (!animeRecentlyUpdated)
            {
                logger.LogInformation("Queuing GET for AniDB_Anime: {AnimeID}", animeID);
                await anidbService.ScheduleRefreshOfAnimeByID(animeID, refreshMethod);
            }

            await scheduler.RunAfterCurrent<RefreshAnimeStatsJob>(b => b.AnimeID = animeID);
        }

        await supplementaryMetadataService.ScheduleForAnimes(animeIDs.Keys, isNew: false);
    }
}

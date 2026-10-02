using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.Abstractions.Utilities;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.QueueProcessor.Scheduling;
using Shoko.QueueProcessor.Workers;
using Shoko.Server.Models.Internal;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.Titles;
using Shoko.Server.Providers.AniDB.UDP.Generic;
using Shoko.Server.Providers.AniDB.UDP.Info;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Server;
using Shoko.Server.Settings;

namespace Shoko.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired]
[AniDBUdpRateLimited]
[DisallowConcurrencyGroup(ConcurrencyGroups.AniDB_UDP)]
[JobKeyGroup(JobKeyGroup.AniDB)]
public class GetUpdatedAniDBAnimeJob(
    IRequestFactory requestFactory,
    IAnidbService anidbService,
    ISettingsProvider settingsProvider,
    AniDBTitleHelper titleHelper,
    AniDB_AnimeRepository anidbAnimeRepository,
    AniDB_AnimeUpdateRepository anidbAnimeUpdates,
    AnimeSeriesRepository animeSeries,
    ScheduledUpdateRepository scheduledUpdates,
    IQueueScheduler scheduler,
    IJobCancellationAccessor cancellation,
    IJobProgressAccessor progress
) : BaseJob
{
    public bool ForceRefresh { get; set; }

    public override string TypeName => "Get Updated AniDB Anime List";

    public override string Title => "Getting Updated AniDB Anime List";

    public override async Task Execute()
    {
        _logger.LogInformation("Processing {Job}", nameof(GetUpdatedAniDBAnimeJob));

        // How often this runs is up to the triggers of the action that queues
        // it. The row only says where the last run left off.
        var schedule = scheduledUpdates.GetByUpdateType((int)ScheduledUpdateType.AniDBUpdates);

        DateTime webUpdateTime;
        if (schedule is null)
        {
            // if this is the first time, lets ask for last 3 days
            webUpdateTime = DateTime.UtcNow.AddDays(-3);

            schedule = new() { UpdateType = (int)ScheduledUpdateType.AniDBUpdates };
        }
        else
        {
            _logger.LogTrace("Last AniDB info update was : {UpdateDetails}", schedule.UpdateDetails);
            webUpdateTime = DateTime.UnixEpoch.AddSeconds(long.Parse(schedule.UpdateDetails));

            _logger.LogInformation("{UpdateTime} since last UPDATED command", DateTime.UtcNow - webUpdateTime);
        }

        // How many pages AniDB has is only known at the end, so each page
        // takes half of what is left.
        var page = 0;
        var (response, countAnime, countSeries) = await Update(webUpdateTime, schedule, 0, 0, PageProgress(page++));

        while (response?.Response?.Count > 200)
        {
            (response, countAnime, countSeries) = await Update(response!.Response.LastUpdated, schedule, countAnime, countSeries, PageProgress(page++));
        }

        progress.Progress.Report(100);

        _logger.LogInformation("Updating {Count} anime records, and {CountSeries} group status records", countAnime,
            countSeries);
    }

    /// <summary>
    /// The share of the progress one page of updates takes: half of what the
    /// pages before it left.
    /// </summary>
    /// <param name="page">The page's index, from 0.</param>
    /// <returns>The page's slice of the job's progress.</returns>
    private RangeProgress PageProgress(int page)
    {
        var left = 100m / (1L << Math.Min(page, 40));
        return new(progress.Progress, 100m - left, 100m - (left / 2));
    }

    private async Task<(UDPResponse<ResponseUpdatedAnime>? response, int countAnime, int countSeries)> Update(
        DateTime webUpdateTime,
        ScheduledUpdate schedule,
        int countAnime,
        int countSeries,
        IProgress<decimal> pageProgress
    )
    {
        // get a list of updates from AniDB
        // startTime will contain the date/time from which the updates apply to
        var request = requestFactory.Create<RequestUpdatedAnime>(r => r.LastUpdated = webUpdateTime);
        var response = await request.SendAsync(cancellation.Token);
        if (response?.Response is null)
        {
            return (null, countAnime, countSeries);
        }

        var animeIDsToUpdate = response.Response.AnimeIDs;
        if (animeIDsToUpdate.Count == 0)
        {
            _logger.LogInformation("No anime to be updated");
            SaveUpdateTime(schedule, response.Response.LastUpdated);
            return (response, countAnime, countSeries);
        }

        var settings = settingsProvider.GetSettings();
        var items = new ItemProgress(pageProgress, animeIDsToUpdate.Count);
        items.Report(0);
        foreach (var animeID in animeIDsToUpdate)
        {
            cancellation.Token.ThrowIfCancellationRequested();
            items.Increment();

            // update the anime from HTTP
            var anime = anidbAnimeRepository.GetByAnimeID(animeID);
            if (anime is null)
            {
                var name = titleHelper.SearchAnimeID(animeID)?.DefaultTitle.Value ?? "<Unknown>";
                if (settings.AniDb.AutomaticallyImportSeries)
                {
                    _logger.LogInformation("Scheduling update for anime: {AnimeTitle} ({AnimeID})", name, animeID);
                    await anidbService.ScheduleRefreshOfAnimeByID(animeID, AnidbRefreshMethod.Remote | AnidbRefreshMethod.DeferToRemoteIfUnsuccessful | AnidbRefreshMethod.CreateShokoSeries).ConfigureAwait(false);
                    countAnime++;
                }
                else
                {
                    _logger.LogTrace("Skipping update for anime because it's not in the local collection: {AnimeTitle} ({AnimeID})", name, animeID);
                }
                continue;
            }

            _logger.LogInformation("Scheduling update for anime: {AnimeTitle} ({AnimeID})", anime.MainTitle, animeID);
            var update = anidbAnimeUpdates.GetByAnimeID(animeID);

            // but only if it hasn't been recently updated
            var ts = DateTime.Now - (update?.UpdatedAt ?? DateTime.UnixEpoch);
            if (ts.TotalHours > 4)
            {
                var refreshMethod = AnidbRefreshMethod.Remote | AnidbRefreshMethod.DeferToRemoteIfUnsuccessful;
                if (settings.AniDb.AutomaticallyImportSeries)
                    refreshMethod |= AnidbRefreshMethod.CreateShokoSeries;
                await anidbService.ScheduleRefreshOfAnimeByID(animeID, refreshMethod).ConfigureAwait(false);
                countAnime++;
            }

            // update the group status
            // this will allow us to determine which anime has missing episodes
            // we only get by an anime where we also have an associated series
            var ser = animeSeries.GetByAnimeID(animeID);
            if (ser is null) continue;

            await anidbService.ScheduleRefreshOfAnimeByID(animeID, AnidbRefreshMethod.Remote | AnidbRefreshMethod.DeferToRemoteIfUnsuccessful).ConfigureAwait(false);
            countSeries++;

            await scheduler.StartJob<GetAniDBReleaseGroupStatusJob>(
                c => (c.AnimeID, c.ForceRefresh) = (animeID, ForceRefresh));
        }

        // Saved once the page is queued, so a cancelled run asks for it again
        // next time.
        SaveUpdateTime(schedule, response.Response.LastUpdated);
        return (response, countAnime, countSeries);
    }

    /// <summary>
    /// Saves where AniDB's list of updates left off, the start of the next
    /// run's.
    /// </summary>
    /// <param name="schedule">The row keeping it.</param>
    /// <param name="lastUpdated">The time of the last update AniDB listed.</param>
    private void SaveUpdateTime(ScheduledUpdate schedule, DateTime lastUpdated)
    {
        schedule.LastUpdate = DateTime.Now;
        schedule.UpdateDetails = ((int)(lastUpdated - DateTime.UnixEpoch).TotalSeconds).ToString();
        scheduledUpdates.Save(schedule);
    }
}

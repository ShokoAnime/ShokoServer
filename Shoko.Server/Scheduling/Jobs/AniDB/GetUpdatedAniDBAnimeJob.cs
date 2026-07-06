using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
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
public class GetUpdatedAniDBAnimeJob(IRequestFactory requestFactory, IAnidbService anidbService, ISettingsProvider settingsProvider, AniDBTitleHelper titleHelper, AniDB_AnimeRepository anidbAnimeRepository, AniDB_AnimeUpdateRepository anidbAnimeUpdates, AnimeSeriesRepository animeSeries, ScheduledUpdateRepository scheduledUpdates) : BaseJob
{
    public bool ForceRefresh { get; set; }

    public override string TypeName => "Get Updated AniDB Anime List";

    public override string Title => "Getting Updated AniDB Anime List";

    public override async Task Execute()
    {
        _logger.LogInformation("Processing {Job}", nameof(GetUpdatedAniDBAnimeJob));

        // check the automated update table to see when the last time we ran this command
        var schedule = scheduledUpdates.GetByUpdateType((int)ScheduledUpdateType.AniDBUpdates);
        if (schedule is not null)
        {
            var settings = settingsProvider.GetSettings();
            var freqHours = settings.AniDb.Anime_UpdateFrequency.Hours;

            // if we have run this in the last 12 hours and are not forcing it, then exit
            var tsLastRun = DateTime.Now - schedule.LastUpdate;
            if (tsLastRun.TotalHours < freqHours && !ForceRefresh) return;
        }

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

        var (response, countAnime, countSeries) = await Update(webUpdateTime, schedule, 0, 0);

        while (response?.Response?.Count > 200)
        {
            (response, countAnime, countSeries) = await Update(response!.Response.LastUpdated, schedule, countAnime, countSeries);
        }

        _logger.LogInformation("Updating {Count} anime records, and {CountSeries} group status records", countAnime,
            countSeries);
    }

    private async Task<(UDPResponse<ResponseUpdatedAnime>? response, int countAnime, int countSeries)> Update(DateTime webUpdateTime, ScheduledUpdate schedule, int countAnime, int countSeries)
    {
        // get a list of updates from AniDB
        // startTime will contain the date/time from which the updates apply to
        var request = requestFactory.Create<RequestUpdatedAnime>(r => r.LastUpdated = webUpdateTime);
        var response = request.Send();
        if (response?.Response is null)
        {
            return (null, countAnime, countSeries);
        }

        var animeIDsToUpdate = response.Response.AnimeIDs;

        // now save the update time from AniDB
        // we will use this next time as a starting point when querying the web cache
        schedule.LastUpdate = DateTime.Now;
        schedule.UpdateDetails = ((int)(response.Response.LastUpdated - DateTime.UnixEpoch).TotalSeconds).ToString();
        scheduledUpdates.Save(schedule);

        if (animeIDsToUpdate.Count == 0)
        {
            _logger.LogInformation("No anime to be updated");
            return (response, countAnime, countSeries);
        }

        var settings = settingsProvider.GetSettings();
        foreach (var animeID in animeIDsToUpdate)
        {
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
        }

        return (response, countAnime, countSeries);
    }
}

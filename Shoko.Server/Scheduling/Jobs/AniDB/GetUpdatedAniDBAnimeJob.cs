using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Models.Server;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.Titles;
using Shoko.Server.Providers.AniDB.UDP.Generic;
using Shoko.Server.Providers.AniDB.UDP.Info;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired]
[AniDBUdpRateLimited]
[DisallowConcurrencyGroup(ConcurrencyGroups.AniDB_UDP)]
[JobKeyGroup(JobKeyGroup.AniDB)]
public class GetUpdatedAniDBAnimeJob : BaseJob
{
    // TODO make this use Quartz scheduling
    private readonly IRequestFactory _requestFactory;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ISettingsProvider _settingsProvider;
    private readonly AniDBTitleHelper _titleHelper;

    public bool ForceRefresh { get; set; }

    public override string TypeName => "Get Updated AniDB Anime List";

    public override string Title => "Getting Updated AniDB Anime List";

    public override async Task Process()
    {
        _logger.LogInformation("Processing {Job}", nameof(GetUpdatedAniDBAnimeJob));

        // check the automated update table to see when the last time we ran this command
        var schedule = RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.AniDBUpdates);
        if (schedule is not null)
        {
            var settings = _settingsProvider.GetSettings();
            var freqHours = Utils.GetScheduledHours(settings.AniDb.Anime_UpdateFrequency);

            // if we have run this in the last 12 hours and are not forcing it, then exit
            var tsLastRun = DateTime.Now - schedule.LastUpdate;
            if (tsLastRun.TotalHours < freqHours && !ForceRefresh) return;
        }

        DateTime webUpdateTime;
        if (schedule is null)
        {
            // if this is the first time, lets ask for last 3 days
            webUpdateTime = DateTime.UtcNow.AddDays(-3);

            schedule = new ScheduledUpdate { UpdateType = (int)ScheduledUpdateType.AniDBUpdates };
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
            (response, countAnime, countSeries) = await Update(response.Response.LastUpdated, schedule, countAnime, countSeries);
        }

        _logger.LogInformation("Updating {Count} anime records, and {CountSeries} group status records", countAnime,
            countSeries);
    }

    private async Task<(UDPResponse<ResponseUpdatedAnime> response, int countAnime, int countSeries)> Update(DateTime webUpdateTime, ScheduledUpdate schedule, int countAnime, int countSeries)
    {
        // get a list of updates from AniDB
        // startTime will contain the date/time from which the updates apply to
        var request = _requestFactory.Create<RequestUpdatedAnime>(r => r.LastUpdated = webUpdateTime);
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
        RepoFactory.ScheduledUpdate.Save(schedule);

        if (animeIDsToUpdate.Count == 0)
        {
            _logger.LogInformation("No anime to be updated");
            return (response, countAnime, countSeries);
        }

        var settings = _settingsProvider.GetSettings();
        foreach (var animeID in animeIDsToUpdate)
        {
            // update the anime from HTTP
            var anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
            if (anime is null)
            {
                var name = _titleHelper.SearchAnimeID(animeID)?.MainTitle ?? "<Unknown>";
                if (settings.AniDb.AutomaticallyImportSeries)
                {
                    _logger.LogInformation("Scheduling update for anime: {AnimeTitle} ({AnimeID})", name, animeID);
                    await (await _schedulerFactory.GetScheduler()).StartJob<GetAniDBAnimeJob>(c =>
                    {
                        c.AnimeID = animeID;
                        c.CreateSeriesEntry = true;
                    });
                    countAnime++;
                }
                else
                {
                    _logger.LogTrace("Skipping update for anime because it's not in the local collection: {AnimeTitle} ({AnimeID})", name, animeID);
                }
                continue;
            }

            _logger.LogInformation("Scheduling update for anime: {AnimeTitle} ({AnimeID})", anime.MainTitle, animeID);
            var update = RepoFactory.AniDB_AnimeUpdate.GetByAnimeID(animeID);

            // but only if it hasn't been recently updated
            var ts = DateTime.Now - (update?.UpdatedAt ?? DateTime.UnixEpoch);
            if (ts.TotalHours > 4)
            {
                await (await _schedulerFactory.GetScheduler()).StartJob<GetAniDBAnimeJob>(c =>
                {
                    c.AnimeID = animeID;
                    c.CreateSeriesEntry = settings.AniDb.AutomaticallyImportSeries;
                });
                countAnime++;
            }

            // update the group status
            // this will allow us to determine which anime has missing episodes
            // we only get by an anime where we also have an associated series
            var ser = RepoFactory.AnimeSeries.GetByAnimeID(animeID);
            if (ser is null) continue;

            await (await _schedulerFactory.GetScheduler()).StartJob<GetAniDBReleaseGroupStatusJob>(c =>
            {
                c.AnimeID = animeID;
                c.ForceRefresh = true;
            });
            countSeries++;
        }

        return (response, countAnime, countSeries);
    }

    public GetUpdatedAniDBAnimeJob(IRequestFactory requestFactory, ISchedulerFactory schedulerFactory, ISettingsProvider settingsProvider, AniDBTitleHelper titleHelper)
    {
        _requestFactory = requestFactory;
        _schedulerFactory = schedulerFactory;
        _settingsProvider = settingsProvider;
        _titleHelper = titleHelper;
    }

    protected GetUpdatedAniDBAnimeJob()
    {
    }
}

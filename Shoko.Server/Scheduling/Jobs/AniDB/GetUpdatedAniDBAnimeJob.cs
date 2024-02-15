using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using QuartzJobFactory.Attributes;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Generic;
using Shoko.Server.Providers.AniDB.UDP.Info;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
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

    public bool ForceRefresh { get; set; }

    public override string Name => "Get Updated AniDB Anime List";
    public override QueueStateStruct Description => new()
    {
        message = "Getting AniDB Anime Updates",
        queueState = QueueStateEnum.GetUpdatedAnime,
        extraParams = Array.Empty<string>()
    };

    public override async Task Process()
    {
        _logger.LogInformation("Processing {Job}", nameof(GetUpdatedAniDBAnimeJob));

        // check the automated update table to see when the last time we ran this command
        var sched = RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.AniDBUpdates);
        if (sched != null)
        {
            var settings = _settingsProvider.GetSettings();
            var freqHours = Utils.GetScheduledHours(settings.AniDb.Anime_UpdateFrequency);

            // if we have run this in the last 12 hours and are not forcing it, then exit
            var tsLastRun = DateTime.Now - sched.LastUpdate;
            if (tsLastRun.TotalHours < freqHours && !ForceRefresh) return;
        }

        DateTime webUpdateTime;
        if (sched == null)
        {
            // if this is the first time, lets ask for last 3 days
            webUpdateTime = DateTime.UtcNow.AddDays(-3);

            sched = new ScheduledUpdate { UpdateType = (int)ScheduledUpdateType.AniDBUpdates };
        }
        else
        {
            _logger.LogTrace("Last AniDB info update was : {UpdateDetails}", sched.UpdateDetails);
            webUpdateTime = DateTime.UnixEpoch.AddSeconds(long.Parse(sched.UpdateDetails));

            _logger.LogInformation("{UpdateTime} since last UPDATED command", DateTime.UtcNow - webUpdateTime);
        }

        var (response, countAnime, countSeries) = await Update(webUpdateTime, sched, 0, 0);

        while (response?.Response?.Count > 200)
        {
            (response, countAnime, countSeries) = await Update(response.Response.LastUpdated, sched, countAnime, countSeries);
        }

        _logger.LogInformation("Updating {Count} anime records, and {CountSeries} group status records", countAnime,
            countSeries);
    }

    private async Task<(UDPResponse<ResponseUpdatedAnime> response, int countAnime, int countSeries)> Update(DateTime webUpdateTime, ScheduledUpdate sched, int countAnime, int countSeries)
    {
        // get a list of updates from AniDB
        // startTime will contain the date/time from which the updates apply to
        var request = _requestFactory.Create<RequestUpdatedAnime>(r => r.LastUpdated = webUpdateTime);
        var response = await request.Send();
        if (response?.Response == null)
        {
            return (null, countAnime, countSeries);
        }

        var animeIDsToUpdate = response.Response.AnimeIDs;

        // now save the update time from AniDB
        // we will use this next time as a starting point when querying the web cache
        sched.LastUpdate = DateTime.Now;
        sched.UpdateDetails = ((int)(response.Response.LastUpdated - DateTime.UnixEpoch).TotalSeconds).ToString();
        RepoFactory.ScheduledUpdate.Save(sched);

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
            if (anime == null)
            {
                _logger.LogTrace("No local record found for Anime ID: {AnimeID}, so skipping...", animeID);
                continue;
            }

            _logger.LogInformation("Updating CommandRequest_GetUpdated: {AnimeID} ", animeID);
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
            // so we only get by an anime where we also have an associated series
            var ser = RepoFactory.AnimeSeries.GetByAnimeID(animeID);
            if (ser == null) continue;

            await (await _schedulerFactory.GetScheduler()).StartJob<GetAniDBReleaseGroupStatusJob>(c =>
            {
                c.AnimeID = animeID;
                c.ForceRefresh = true;
            });
            countSeries++;
        }

        return (response, countAnime, countSeries);
    }
    
    public GetUpdatedAniDBAnimeJob(IRequestFactory requestFactory, ISchedulerFactory schedulerFactory, ISettingsProvider settingsProvider)
    {
        _requestFactory = requestFactory;
        _schedulerFactory = schedulerFactory;
        _settingsProvider = settingsProvider;
    }

    protected GetUpdatedAniDBAnimeJob()
    {
    }
}

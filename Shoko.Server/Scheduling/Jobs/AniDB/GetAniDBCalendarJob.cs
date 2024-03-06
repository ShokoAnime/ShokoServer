using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Providers.AniDB.Interfaces;
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
public class GetAniDBCalendarJob : BaseJob
{
    private readonly IRequestFactory _requestFactory;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ISettingsProvider _settingsProvider;
    public bool ForceRefresh { get; set; }

    public override string Name => "Get AniDB Calendar";
    public override QueueStateStruct Description => new()
    {
        message = "Getting AniDB Calendar",
        queueState = QueueStateEnum.GetCalendar,
        extraParams = Array.Empty<string>()
    };

    public override async Task Process()
    {
        _logger.LogInformation("Processing CommandRequest_GetCalendar");

        var settings = _settingsProvider.GetSettings();
        // we will always assume that an anime was downloaded via http first

        var sched =
            RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.AniDBCalendar);
        if (sched == null)
        {
            sched = new ScheduledUpdate
            {
                UpdateType = (int)ScheduledUpdateType.AniDBCalendar, UpdateDetails = string.Empty
            };
        }
        else
        {
            var freqHours = Utils.GetScheduledHours(settings.AniDb.Calendar_UpdateFrequency);

            // if we have run this in the last 12 hours and are not forcing it, then exit
            var tsLastRun = DateTime.Now - sched.LastUpdate;
            if (tsLastRun.TotalHours < freqHours)
            {
                if (!ForceRefresh) return;
            }
        }

        sched.LastUpdate = DateTime.Now;

        var request = _requestFactory.Create<RequestCalendar>();
        var response = request.Send();
        RepoFactory.ScheduledUpdate.Save(sched);

        if (response.Response?.Next25Anime != null)
        {
            foreach (var cal in response.Response.Next25Anime)
            {
                await GetAnime(cal, settings);
            }
        }

        if (response.Response?.Previous25Anime == null) return;

        foreach (var cal in response.Response.Previous25Anime)
        {
            await GetAnime(cal, settings);
        }
    }

    private async Task GetAnime(ResponseCalendar.CalendarEntry cal, IServerSettings settings)
    {
        var anime = RepoFactory.AniDB_Anime.GetByAnimeID(cal.AnimeID);
        var update = RepoFactory.AniDB_AnimeUpdate.GetByAnimeID(cal.AnimeID);
        if (anime != null && update != null)
        {
            // don't update if the local data is less 2 days old
            var ts = DateTime.Now - update.UpdatedAt;
            if (ts.TotalDays >= 2)
            {
                await (await _schedulerFactory.GetScheduler()).StartJob<GetAniDBAnimeJob>(
                    c =>
                    {
                        c.AnimeID = cal.AnimeID;
                        c.DownloadRelations = settings.AniDb.DownloadRelatedAnime;
                        c.ForceRefresh = true;
                        c.CreateSeriesEntry = settings.AniDb.AutomaticallyImportSeries;
                    });
            }
            else
            {
                // update the release date even if we don't update the anime record
                if (anime.AirDate == cal.ReleaseDate) return;

                anime.AirDate = cal.ReleaseDate;
                RepoFactory.AniDB_Anime.Save(anime);
                var ser = RepoFactory.AnimeSeries.GetByAnimeID(anime.AnimeID);
                if (ser != null) RepoFactory.AnimeSeries.Save(ser, true, false);
            }
        }
        else
        {
            await (await _schedulerFactory.GetScheduler()).StartJob<GetAniDBAnimeJob>(
                c =>
                {
                    c.AnimeID = cal.AnimeID;
                    c.DownloadRelations = settings.AniDb.DownloadRelatedAnime;
                    c.ForceRefresh = true;
                    c.CreateSeriesEntry = settings.AniDb.AutomaticallyImportSeries;
                });
        }
    }
    
    public GetAniDBCalendarJob(IRequestFactory requestFactory,
        ISchedulerFactory schedulerFactory, ISettingsProvider settingsProvider)
    {
        _requestFactory = requestFactory;
        _schedulerFactory = schedulerFactory;
        _settingsProvider = settingsProvider;
    }

    protected GetAniDBCalendarJob()
    {
    }
}

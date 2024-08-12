using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
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

    public override string TypeName => "Get AniDB Calendar";

    public override string Title => "Getting AniDB Calendar";

    public override async Task Process()
    {
        _logger.LogInformation("Processing {Job}", nameof(GetAniDBCalendarJob));

        var settings = _settingsProvider.GetSettings();
        // we will always assume that an anime was downloaded via http first

        var schedule = RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.AniDBCalendar);
        if (schedule is null)
        {
            schedule = new ScheduledUpdate
            {
                UpdateType = (int)ScheduledUpdateType.AniDBCalendar,
                UpdateDetails = string.Empty,
            };
        }
        else
        {
            var freqHours = Utils.GetScheduledHours(settings.AniDb.Calendar_UpdateFrequency);

            // if we have run this in the last 12 hours and are not forcing it, then exit
            var tsLastRun = DateTime.Now - schedule.LastUpdate;
            if (tsLastRun.TotalHours < freqHours)
            {
                if (!ForceRefresh) return;
            }
        }

        schedule.LastUpdate = DateTime.Now;

        var request = _requestFactory.Create<RequestCalendar>();
        var response = request.Send();
        RepoFactory.ScheduledUpdate.Save(schedule);

        var scheduler = await _schedulerFactory.GetScheduler();
        if (response.Response?.Next25Anime is not null)
        {
            foreach (var cal in response.Response.Next25Anime)
            {
                if (cal.AnimeID == 0) continue;
                await GetAnime(scheduler, cal, settings);
            }
        }

        if (response.Response?.Previous25Anime is null) return;

        foreach (var cal in response.Response.Previous25Anime)
        {
            if (cal.AnimeID == 0) continue;
            await GetAnime(scheduler, cal, settings);
        }
    }

    private static async Task GetAnime(IScheduler scheduler, ResponseCalendar.CalendarEntry cal, IServerSettings settings)
    {
        var anime = RepoFactory.AniDB_Anime.GetByAnimeID(cal.AnimeID);
        var update = RepoFactory.AniDB_AnimeUpdate.GetByAnimeID(cal.AnimeID);
        if (anime is not null && update is not null)
        {
            // don't update if the local data is less 2 days old
            var ts = DateTime.Now - update.UpdatedAt;
            if (ts.TotalDays >= 2)
            {
                await scheduler.StartJob<GetAniDBAnimeJob>(
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
                if (ser is not null) RepoFactory.AnimeSeries.Save(ser, true, false);
            }
        }
        else
        {
            await scheduler.StartJob<GetAniDBAnimeJob>(
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

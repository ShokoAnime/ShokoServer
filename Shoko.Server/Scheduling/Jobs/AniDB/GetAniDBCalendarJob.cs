using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Services;
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
    private readonly IAnidbService _anidbService;
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
            schedule = new()
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

        if (response.Response?.Next25Anime is not null)
        {
            foreach (var cal in response.Response.Next25Anime)
            {
                if (cal.AnimeID == 0) continue;
                await GetAnime(cal, settings);
            }
        }

        if (response.Response?.Previous25Anime is null) return;

        foreach (var cal in response.Response.Previous25Anime)
        {
            if (cal.AnimeID == 0) continue;
            await GetAnime(cal, settings);
        }
    }

    private async Task GetAnime(ResponseCalendar.CalendarEntry cal, IServerSettings settings)
    {
        var anime = RepoFactory.AniDB_Anime.GetByAnimeID(cal.AnimeID);
        var update = RepoFactory.AniDB_AnimeUpdate.GetByAnimeID(cal.AnimeID);
        var refreshMethod = AnidbRefreshMethod.Remote | AnidbRefreshMethod.DeferToRemoteIfUnsuccessful;
        if (settings.AutoGroupSeries || settings.AniDb.DownloadRelatedAnime)
            refreshMethod |= AnidbRefreshMethod.DownloadRelations;
        if (settings.AniDb.AutomaticallyImportSeries)
            refreshMethod |= AnidbRefreshMethod.CreateShokoSeries;
        if (anime is not null && update is not null)
        {
            // don't update if the local data is less 2 days old
            var ts = DateTime.Now - update.UpdatedAt;
            if (ts.TotalDays >= 2)
            {
                await _anidbService.ScheduleRefreshByID(cal.AnimeID, refreshMethod).ConfigureAwait(false);
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
            await _anidbService.ScheduleRefreshByID(cal.AnimeID, refreshMethod).ConfigureAwait(false);
        }
    }

    public GetAniDBCalendarJob(IRequestFactory requestFactory,
        IAnidbService anidbService, ISettingsProvider settingsProvider)
    {
        _requestFactory = requestFactory;
        _anidbService = anidbService;
        _settingsProvider = settingsProvider;
    }

    protected GetAniDBCalendarJob()
    {
    }
}

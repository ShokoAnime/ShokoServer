using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Providers.AniDB.Interfaces;
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
public class GetAniDBCalendarJob(IRequestFactory requestFactory, IAnidbService anidbService, ISettingsProvider settingsProvider, AniDB_AnimeRepository anidbAnime, AniDB_AnimeUpdateRepository anidbAnimeUpdates, AnimeSeriesRepository animeSeries, ScheduledUpdateRepository scheduledUpdates) : BaseJob
{
    public bool ForceRefresh { get; set; }

    public override string TypeName => "Get AniDB Calendar";

    public override string Title => "Getting AniDB Calendar";

    public override async Task Execute()
    {
        _logger.LogInformation("Processing {Job}", nameof(GetAniDBCalendarJob));

        var settings = settingsProvider.GetSettings();
        // we will always assume that an anime was downloaded via http first

        var schedule = scheduledUpdates.GetByUpdateType((int)ScheduledUpdateType.AniDBCalendar);
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
            var freqHours = settings.AniDb.Calendar_UpdateFrequency.Hours;

            // if we have run this in the last 12 hours and are not forcing it, then exit
            var tsLastRun = DateTime.Now - schedule.LastUpdate;
            if (tsLastRun.TotalHours < freqHours)
            {
                if (!ForceRefresh) return;
            }
        }

        schedule.LastUpdate = DateTime.Now;

        var request = requestFactory.Create<RequestCalendar>();
        var response = request.Send();
        scheduledUpdates.Save(schedule);

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
        var anime = anidbAnime.GetByAnimeID(cal.AnimeID);
        var update = anidbAnimeUpdates.GetByAnimeID(cal.AnimeID);
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
                await anidbService.ScheduleRefreshOfAnimeByID(cal.AnimeID, refreshMethod).ConfigureAwait(false);
            }
            else
            {
                // update the release date even if we don't update the anime record
                var releaseDate = PartialDateOnly.FromDateTime(cal.ReleaseDate);
                if (anime.AirDate == releaseDate) return;

                anime.AirDate = releaseDate;
                anidbAnime.Save(anime);
                var ser = animeSeries.GetByAnimeID(anime.AnimeID);
                if (ser is not null) animeSeries.Save(ser, true);
            }
        }
        else
        {
            await anidbService.ScheduleRefreshOfAnimeByID(cal.AnimeID, refreshMethod).ConfigureAwait(false);
        }
    }
}

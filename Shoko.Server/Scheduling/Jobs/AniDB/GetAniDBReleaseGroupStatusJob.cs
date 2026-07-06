using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.Titles;
using Shoko.Server.Providers.AniDB.UDP.Info;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Settings;

namespace Shoko.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired]
[AniDBUdpRateLimited]
[DisallowConcurrencyGroup(ConcurrencyGroups.AniDB_UDP)]
[JobKeyGroup(JobKeyGroup.AniDB)]
public class GetAniDBReleaseGroupStatusJob(IRequestFactory requestFactory, IAnidbService anidbService, ISettingsProvider settingsProvider, AniDBTitleHelper titleHelper, IQueueScheduler scheduler, AniDB_AnimeRepository anidbAnimes, AniDB_EpisodeRepository anidbEpisodes, AniDB_GroupStatusRepository anidbGroupStatuses, AnimeSeriesRepository animeSeries) : BaseJob
{
    private AniDB_Anime _anime;
    private string _animeName;
    public int AnimeID { get; set; }
    public bool ForceRefresh { get; set; }

    public override string TypeName => "Get AniDB Release Group Status for Anime";
    public override string Title => "Getting AniDB Release Group Status for Anime";

    public override void PostInit()
    {
        _anime = anidbAnimes.GetByAnimeID(AnimeID);
        _animeName = _anime?.Title ?? titleHelper.SearchAnimeID(AnimeID)?.Title;
    }

    public override Dictionary<string, object> Details => _animeName == null ? new()
    {
        {
            "AnimeID", AnimeID
        }
    } : new() {
        {
            "Anime", _animeName
        }
    };

    public override async Task Execute()
    {
        _logger.LogInformation("Processing {Job}: {GroupID}", nameof(GetAniDBReleaseGroupStatusJob), AnimeID);

        if (AnimeID == 0) return;
        // only get group status if we have an associated series
        var series = animeSeries.GetByAnimeID(AnimeID);
        if (series == null) return;

        var anime = anidbAnimes.GetByAnimeID(AnimeID);
        if (anime == null) return;

        // don't get group status if the anime has already ended more than 50 days ago
        if (ShouldSkip(anime))
        {
            _logger.LogInformation("Skipping group status command because anime has already ended: {AnimeID}",
                AnimeID);
            return;
        }

        var request = requestFactory.Create<RequestReleaseGroupStatus>(r => r.AnimeID = AnimeID);
        var response = request.Send();
        if (response.Response == null) return;

        var maxEpisode = response.Response.Max(a => a.LastEpisodeNumber);

        // delete existing records
        anidbGroupStatuses.DeleteForAnime(AnimeID);

        // save the records
        var toSave = response.Response.Select(
            raw => new AniDB_GroupStatus
            {
                AnimeID = raw.AnimeID,
                GroupID = raw.GroupID,
                GroupName = raw.GroupName,
                CompletionState = (int)raw.CompletionState,
                LastEpisodeNumber = raw.LastEpisodeNumber,
                Rating = raw.Rating,
                Votes = raw.Votes,
                EpisodeRange = string.Join(',', raw.ReleasedEpisodes)
            }
        ).ToArray();
        anidbGroupStatuses.Save(toSave);
        await scheduler.RunAfterCurrent<RefreshAnimeStatsJob>(a => a.AnimeID = AnimeID).ConfigureAwait(false);

        var settings = settingsProvider.GetSettings();
        if (maxEpisode > 0)
        {
            // update the anime with a record of the latest subbed episode
            anime.LatestEpisodeNumber = maxEpisode;
            anidbAnimes.Save(anime);

            // check if we have this episode in the database
            // if not get it now by updating the anime record
            var eps = anidbEpisodes.GetByAnimeIDAndEpisodeNumber(AnimeID, maxEpisode);
            if (eps.Count == 0)
            {
                var refreshMethod = AnidbRefreshMethod.Remote | AnidbRefreshMethod.DeferToRemoteIfUnsuccessful;
                if (settings.AniDb.AutomaticallyImportSeries)
                    refreshMethod |= AnidbRefreshMethod.CreateShokoSeries;
                await anidbService.ScheduleRefreshOfAnimeByID(AnimeID, refreshMethod).ConfigureAwait(false);
            }
        }
    }

    private bool ShouldSkip(AniDB_Anime anime)
    {
        if (ForceRefresh)
        {
            return false;
        }

        if (!anime.EndDate.HasValue)
        {
            return false;
        }

        if (anime.EndDate.Value >= DateTime.Now)
        {
            return false;
        }

        var ts = DateTime.Now - anime.EndDate.Value;
        if (!(ts.TotalDays > 50))
        {
            return false;
        }

        // don't skip if we have never downloaded this info before
        var grpStatuses = anidbGroupStatuses.GetByAnimeID(AnimeID);
        return grpStatuses is { Count: > 0 };
    }


}

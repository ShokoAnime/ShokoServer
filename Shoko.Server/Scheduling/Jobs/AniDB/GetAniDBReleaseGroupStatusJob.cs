using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.Titles;
using Shoko.Server.Providers.AniDB.UDP.Info;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Settings;

namespace Shoko.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired]
[AniDBUdpRateLimited]
[DisallowConcurrencyGroup(ConcurrencyGroups.AniDB_UDP)]
[JobKeyGroup(JobKeyGroup.AniDB)]
public class GetAniDBReleaseGroupStatusJob : BaseJob
{
    private readonly AniDBTitleHelper _titleHelper;
    private readonly IRequestFactory _requestFactory;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ISettingsProvider _settingsProvider;
    private SVR_AniDB_Anime _anime;
    private string _animeName;
    public int AnimeID { get; set; }
    public bool ForceRefresh { get; set; }

    public override string TypeName => "Get AniDB Release Group Status for Anime";
    public override string Title => "Getting AniDB Release Group Status for Anime";

    public override void PostInit()
    {
        _anime = RepoFactory.AniDB_Anime?.GetByAnimeID(AnimeID);
        _animeName = _anime?.PreferredTitle ?? _titleHelper.SearchAnimeID(AnimeID)?.PreferredTitle;
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

    public override async Task Process()
    {
        _logger.LogInformation("Processing {Job}: {GroupID}", nameof(GetAniDBReleaseGroupJob), AnimeID);

        if (AnimeID == 0) return;
        // only get group status if we have an associated series
        var series = RepoFactory.AnimeSeries.GetByAnimeID(AnimeID);
        if (series == null) return;

        var anime = RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID);
        if (anime == null) return;

        // don't get group status if the anime has already ended more than 50 days ago
        if (ShouldSkip(anime))
        {
            _logger.LogInformation("Skipping group status command because anime has already ended: {AnimeID}",
                AnimeID);
            return;
        }

        var request = _requestFactory.Create<RequestReleaseGroupStatus>(r => r.AnimeID = AnimeID);
        var response = request.Send();
        if (response.Response == null) return;

        var maxEpisode = response.Response.Max(a => a.LastEpisodeNumber);

        // delete existing records
        RepoFactory.AniDB_GroupStatus.DeleteForAnime(AnimeID);

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
        RepoFactory.AniDB_GroupStatus.Save(toSave);

        var settings = _settingsProvider.GetSettings();
        var scheduler = await _schedulerFactory.GetScheduler();
        if (maxEpisode > 0)
        {
            // update the anime with a record of the latest subbed episode
            anime.LatestEpisodeNumber = maxEpisode;
            RepoFactory.AniDB_Anime.Save(anime, false);

            // check if we have this episode in the database
            // if not get it now by updating the anime record
            var eps = RepoFactory.AniDB_Episode.GetByAnimeIDAndEpisodeNumber(AnimeID, maxEpisode);
            if (eps.Count == 0)
            {
                await scheduler.StartJobNow<GetAniDBAnimeJob>(c =>
                {
                    c.AnimeID = AnimeID;
                    c.ForceRefresh = true;
                    c.CreateSeriesEntry = settings.AniDb.AutomaticallyImportSeries;
                });
            }

            // update the missing episode stats on groups and children
            series.QueueUpdateStats();
        }

        if (settings.AniDb.DownloadReleaseGroups && response is { Response.Count: > 0 })
        {
            // shouldn't need the where, but better safe than sorry.
            foreach(var g in response.Response.DistinctBy(a => a.GroupID).Where(a => a.GroupID != 0))
            {
                await scheduler.StartJob<GetAniDBReleaseGroupJob>(c => c.GroupID = g.GroupID);
            }
        }
    }

    private bool ShouldSkip(SVR_AniDB_Anime anime)
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
        var grpStatuses = RepoFactory.AniDB_GroupStatus.GetByAnimeID(AnimeID);
        return grpStatuses is { Count: > 0 };
    }

    public GetAniDBReleaseGroupStatusJob(IRequestFactory requestFactory, ISchedulerFactory schedulerFactory, ISettingsProvider settingsProvider, AniDBTitleHelper titleHelper)
    {
        _requestFactory = requestFactory;
        _schedulerFactory = schedulerFactory;
        _settingsProvider = settingsProvider;
        _titleHelper = titleHelper;
    }

    protected GetAniDBReleaseGroupStatusJob() { }
}

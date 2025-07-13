using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Quartz;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.AniDB;

namespace Shoko.Server.Services;

public class AnimeEpisodeService
{
    private readonly AnimeEpisode_UserRepository _epUsers;
    private readonly AnimeSeries_UserRepository _seriesUsers;
    private readonly VideoLocalRepository _videoLocals;
    private readonly VideoLocalService _vlService;
    private readonly ISchedulerFactory _schedulerFactory;

    public AnimeEpisodeService(AnimeEpisode_UserRepository episodeUsers, AnimeSeries_UserRepository seriesUsers, VideoLocalRepository videoLocals, VideoLocalService vlService, ISchedulerFactory schedulerFactory)
    {
        _epUsers = episodeUsers;
        _seriesUsers = seriesUsers;
        _videoLocals = videoLocals;
        _vlService = vlService;
        _schedulerFactory = schedulerFactory;
    }

    public async Task AddEpisodeVote(SVR_AnimeEpisode episode, decimal vote)
    {
        var dbVote = RepoFactory.AniDB_Vote.GetByEntityAndType(episode.AniDB_EpisodeID, AniDBVoteType.Episode) ??
                     new AniDB_Vote { EntityID = episode.AniDB_EpisodeID, VoteType = (int)AniDBVoteType.Episode };
        dbVote.VoteValue = vote < 0 ? -1 : (int)Math.Floor(vote * 100);

        RepoFactory.AniDB_Vote.Save(dbVote);

        var scheduler = await _schedulerFactory.GetScheduler();
        await scheduler.StartJob<VoteAniDBEpisodeJob>(c =>
        {
            c.EpisodeID = episode.AniDB_EpisodeID;
            c.VoteValue = Convert.ToDouble(vote);
        });
    }

    public List<CL_VideoDetailed> GetV1VideoDetailedContracts(SVR_AnimeEpisode ep, int userID)
    {
        // get all the cross refs
        return ep?.FileCrossReferences
            .Select(xref => xref.VideoLocal)
            .Where(v => v != null)
            .Select(v => _vlService.GetV1DetailedContract(v, userID)).ToList() ?? [];
    }

    public CL_AnimeEpisode_User GetV1Contract(SVR_AnimeEpisode ep, int userID)
    {
        if (ep == null) return null;
        var anidbEpisode = ep.AniDB_Episode ?? throw new NullReferenceException($"Unable to find AniDB Episode with id {ep.AniDB_EpisodeID} locally while generating user contract for shoko episode.");
        var seriesUserRecord = _seriesUsers.GetByUserAndSeriesID(userID, ep.AnimeSeriesID);
        var episodeUserRecord = _epUsers.GetByUserIDAndEpisodeID(userID, ep.AnimeEpisodeID);
        var contract = new CL_AnimeEpisode_User
        {
            AniDB_EpisodeID = ep.AniDB_EpisodeID,
            AnimeEpisodeID = ep.AnimeEpisodeID,
            AnimeSeriesID = ep.AnimeSeriesID,
            DateTimeCreated = ep.DateTimeCreated,
            DateTimeUpdated = ep.DateTimeUpdated,
            PlayedCount = episodeUserRecord?.PlayedCount ?? 0,
            StoppedCount = episodeUserRecord?.StoppedCount ?? 0,
            WatchedCount = episodeUserRecord?.WatchedCount ?? 0,
            WatchedDate = episodeUserRecord?.WatchedDate,
            AniDB_EnglishName = RepoFactory.AniDB_Episode_Title.GetByEpisodeIDAndLanguage(ep.AniDB_EpisodeID, TitleLanguage.English)
                .FirstOrDefault()?.Title,
            AniDB_RomajiName = RepoFactory.AniDB_Episode_Title.GetByEpisodeIDAndLanguage(ep.AniDB_EpisodeID, TitleLanguage.Romaji)
                .FirstOrDefault()?.Title,
            AniDB_AirDate = anidbEpisode.GetAirDateAsDate(),
            AniDB_LengthSeconds = anidbEpisode.LengthSeconds,
            AniDB_Rating = anidbEpisode.Rating,
            AniDB_Votes = anidbEpisode.Votes,
            EpisodeNumber = anidbEpisode.EpisodeNumber,
            Description = anidbEpisode.Description,
            EpisodeType = anidbEpisode.EpisodeType,
            UnwatchedEpCountSeries = seriesUserRecord?.UnwatchedEpisodeCount ?? 0,
            LocalFileCount = ep.VideoLocals.Count,
        };
        return contract;
    }
}

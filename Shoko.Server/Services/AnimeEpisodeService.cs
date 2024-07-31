using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Commons.Extensions;
using Shoko.Models.Client;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;

namespace Shoko.Server.Services;

public class AnimeEpisodeService
{
    private readonly AnimeEpisode_UserRepository _epUsers;
    private readonly AnimeSeries_UserRepository _seriesUsers;
    private readonly VideoLocalRepository _videoLocals;
    private readonly VideoLocalService _vlService;

    public AnimeEpisodeService(AnimeEpisode_UserRepository episodeUsers, AnimeSeries_UserRepository seriesUsers, VideoLocalRepository videoLocals, VideoLocalService vlService)
    {
        _epUsers = episodeUsers;
        _seriesUsers = seriesUsers;
        _videoLocals = videoLocals;
        _vlService = vlService;
    }

    public List<CL_VideoDetailed> GetV1VideoDetailedContracts(SVR_AnimeEpisode ep, int userID)
    {
        // get all the cross refs
        return ep?.FileCrossReferences.Select(xref => _videoLocals.GetByHash(xref.Hash))
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

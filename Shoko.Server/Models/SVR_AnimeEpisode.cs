using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Commons.Extensions;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;
using AnimeTitle = Shoko.Plugin.Abstractions.DataModels.AnimeTitle;
using EpisodeType = Shoko.Models.Enums.EpisodeType;

namespace Shoko.Server.Models;

public class SVR_AnimeEpisode : AnimeEpisode
{
    public EpisodeType EpisodeTypeEnum => (EpisodeType)AniDB_Episode.EpisodeType;

    public SVR_AniDB_Episode AniDB_Episode => RepoFactory.AniDB_Episode.GetByEpisodeID(AniDB_EpisodeID);

    public SVR_AnimeEpisode_User GetUserRecord(int userID)
    {
        return RepoFactory.AnimeEpisode_User.GetByUserIDAndEpisodeID(userID, AnimeEpisodeID);
    }

    /// <summary>
    /// Gets the AnimeSeries this episode belongs to
    /// </summary>
    public SVR_AnimeSeries GetAnimeSeries()
    {
        return RepoFactory.AnimeSeries.GetByID(AnimeSeriesID);
    }

    public List<SVR_VideoLocal> GetVideoLocals(CrossRefSource? xrefSource = null)
    {
        return RepoFactory.VideoLocal.GetByAniDBEpisodeID(AniDB_EpisodeID, xrefSource);
    }

    public List<CrossRef_File_Episode> FileCrossRefs => RepoFactory.CrossRef_File_Episode.GetByEpisodeID(AniDB_EpisodeID);

    public TvDB_Episode TvDBEpisode
    {
        get
        {
            // Try Overrides first, then regular
            return RepoFactory.CrossRef_AniDB_TvDB_Episode_Override.GetByAniDBEpisodeID(AniDB_EpisodeID)
                .Select(a => RepoFactory.TvDB_Episode.GetByTvDBID(a.TvDBEpisodeID)).Where(a => a != null)
                .OrderBy(a => a.SeasonNumber).ThenBy(a => a.EpisodeNumber).FirstOrDefault() ?? RepoFactory.CrossRef_AniDB_TvDB_Episode.GetByAniDBEpisodeID(AniDB_EpisodeID)
                .Select(a => RepoFactory.TvDB_Episode.GetByTvDBID(a.TvDBEpisodeID)).Where(a => a != null)
                .OrderBy(a => a.SeasonNumber).ThenBy(a => a.EpisodeNumber).FirstOrDefault();
        }
    }

    public List<TvDB_Episode> TvDBEpisodes
    {
        get
        {
            // Try Overrides first, then regular
            var overrides = RepoFactory.CrossRef_AniDB_TvDB_Episode_Override.GetByAniDBEpisodeID(AniDB_EpisodeID)
                .Select(a => RepoFactory.TvDB_Episode.GetByTvDBID(a.TvDBEpisodeID)).Where(a => a != null)
                .OrderBy(a => a.SeasonNumber).ThenBy(a => a.EpisodeNumber).ToList();
            return overrides.Count > 0
                ? overrides
                : RepoFactory.CrossRef_AniDB_TvDB_Episode.GetByAniDBEpisodeID(AniDB_EpisodeID)
                    .Select(a => RepoFactory.TvDB_Episode.GetByTvDBID(a.TvDBEpisodeID)).Where(a => a != null)
                    .OrderBy(a => a.SeasonNumber).ThenBy(a => a.EpisodeNumber).ToList();
        }
    }

    public double UserRating
    {
        get
        {
            AniDB_Vote vote = RepoFactory.AniDB_Vote.GetByEntityAndType(AnimeEpisodeID, AniDBVoteType.Episode);
            if (vote != null) return vote.VoteValue / 100D;
            return -1;
        }
    }

    public void SaveWatchedStatus(bool watched, int userID, DateTime? watchedDate, bool updateWatchedDate)
    {

        var epUserRecord = GetUserRecord(userID);

        if (watched)
        {
            // lets check if an update is actually required
            if (epUserRecord?.WatchedDate != null && watchedDate.HasValue &&
                epUserRecord.WatchedDate.Equals(watchedDate.Value) ||
                (epUserRecord?.WatchedDate == null && !watchedDate.HasValue))
                return;

            if (epUserRecord == null)
                epUserRecord = new SVR_AnimeEpisode_User(userID, AnimeEpisodeID, AnimeSeriesID);
            epUserRecord.WatchedCount++;

            if (epUserRecord.WatchedDate.HasValue && updateWatchedDate || !epUserRecord.WatchedDate.HasValue)
                epUserRecord.WatchedDate = watchedDate ?? DateTime.Now;

            RepoFactory.AnimeEpisode_User.Save(epUserRecord);
        }
        else if (epUserRecord != null && updateWatchedDate)
        {
            epUserRecord.WatchedDate = null;
            RepoFactory.AnimeEpisode_User.Save(epUserRecord);
        }
    }


    public List<CL_VideoDetailed> GetVideoDetailedContracts(int userID)
    {
        // get all the cross refs
        return FileCrossRefs.Select(xref => RepoFactory.VideoLocal.GetByHash(xref.Hash))
            .Where(v => v != null)
            .Select(v => v.ToClientDetailed(userID)).ToList();
    }

    public CL_AnimeEpisode_User GetUserContract(int userID)
    {
        var anidbEpisode = AniDB_Episode;
        var seriesUserRecord = RepoFactory.AnimeSeries_User.GetByUserAndSeriesID(userID, AnimeSeriesID);
        var episodeUserRecord = GetUserRecord(userID);
        var contract = new CL_AnimeEpisode_User
        {
            AniDB_EpisodeID = AniDB_EpisodeID,
            AnimeEpisodeID = AnimeEpisodeID,
            AnimeSeriesID = AnimeSeriesID,
            DateTimeCreated = DateTimeCreated,
            DateTimeUpdated = DateTimeUpdated,
            PlayedCount = episodeUserRecord?.PlayedCount ?? 0,
            StoppedCount = episodeUserRecord?.StoppedCount ?? 0,
            WatchedCount = episodeUserRecord?.WatchedCount ?? 0,
            WatchedDate = episodeUserRecord?.WatchedDate,
            AniDB_EnglishName = RepoFactory.AniDB_Episode_Title.GetByEpisodeIDAndLanguage(AniDB_EpisodeID, TitleLanguage.English)
                .FirstOrDefault()?.Title,
            AniDB_RomajiName = RepoFactory.AniDB_Episode_Title.GetByEpisodeIDAndLanguage(AniDB_EpisodeID, TitleLanguage.Romaji)
                .FirstOrDefault()?.Title,
            AniDB_AirDate = anidbEpisode.GetAirDateAsDate(),
            AniDB_LengthSeconds = anidbEpisode.LengthSeconds,
            AniDB_Rating = anidbEpisode.Rating,
            AniDB_Votes = anidbEpisode.Votes,
            EpisodeNumber = anidbEpisode.EpisodeNumber,
            Description = anidbEpisode.Description,
            EpisodeType = anidbEpisode.EpisodeType,
            UnwatchedEpCountSeries = seriesUserRecord?.UnwatchedEpisodeCount ?? 0,
            LocalFileCount = GetVideoLocals().Count,
        };
        return contract;
    }

    public void ToggleWatchedStatus(bool watched, bool updateOnline, DateTime? watchedDate, int userID,
        bool syncTrakt)
    {
        ToggleWatchedStatus(watched, updateOnline, watchedDate, true, userID, syncTrakt);
    }

    public void ToggleWatchedStatus(bool watched, bool updateOnline, DateTime? watchedDate, bool updateStats,
        int userID, bool syncTrakt)
    {
        foreach (SVR_VideoLocal vid in GetVideoLocals())
        {
            vid.ToggleWatchedStatus(watched, updateOnline, watchedDate, updateStats, userID,
                syncTrakt, true);
            vid.SetResumePosition(0, userID);
        }
    }
        
    public void RemoveVideoLocals(bool deleteFiles)
    {
        
    }

    public string Title
    {
        get
        {
            // Try finding one of the preferred languages.
            foreach (var language in Languages.PreferredEpisodeNamingLanguages)
            {
                var title = RepoFactory.AniDB_Episode_Title.GetByEpisodeIDAndLanguage(AniDB_EpisodeID, language.Language)
                    .FirstOrDefault()
                    ?.Title;
                if (!string.IsNullOrEmpty(title))
                    return title;
            }

            // Fallback to English if available.
            return RepoFactory.AniDB_Episode_Title.GetByEpisodeIDAndLanguage(AniDB_EpisodeID, TitleLanguage.English)
                .FirstOrDefault()
                ?.Title;
        }
    }

    protected bool Equals(SVR_AnimeEpisode other)
    {
        return AnimeEpisodeID == other.AnimeEpisodeID && AnimeSeriesID == other.AnimeSeriesID &&
               AniDB_EpisodeID == other.AniDB_EpisodeID && DateTimeUpdated.Equals(other.DateTimeUpdated) &&
               DateTimeCreated.Equals(other.DateTimeCreated);
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != this.GetType())
        {
            return false;
        }

        return Equals((SVR_AnimeEpisode)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = AnimeEpisodeID;
            hashCode = (hashCode * 397) ^ AnimeSeriesID;
            hashCode = (hashCode * 397) ^ AniDB_EpisodeID;
            hashCode = (hashCode * 397) ^ DateTimeUpdated.GetHashCode();
            hashCode = (hashCode * 397) ^ DateTimeCreated.GetHashCode();
            return hashCode;
        }
    }
}

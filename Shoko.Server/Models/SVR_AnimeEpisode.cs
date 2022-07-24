using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Commons.Extensions;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.PlexAndKodi;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Server.PlexAndKodi;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Utilities;
using AnimeTitle = Shoko.Plugin.Abstractions.DataModels.AnimeTitle;
using EpisodeType = Shoko.Models.Enums.EpisodeType;

namespace Shoko.Server.Models
{
    public class SVR_AnimeEpisode : AnimeEpisode, IEpisode
    {
        public EpisodeType EpisodeTypeEnum => (EpisodeType) AniDB_Episode.EpisodeType;

        public AniDB_Episode AniDB_Episode => RepoFactory.AniDB_Episode.GetByEpisodeID(AniDB_EpisodeID);

        public SVR_AnimeEpisode_User GetUserRecord(int userID)
        {
            return RepoFactory.AnimeEpisode_User.GetByUserIDAndEpisodeID(userID, AnimeEpisodeID);
        }

        public SVR_AnimeEpisode_User GetOrCreateUserRecord(int userID, ISessionWrapper session = null)
        {
            var userRecord = GetUserRecord(userID);
            if (userRecord != null)
                return userRecord;
            userRecord = new SVR_AnimeEpisode_User(userID, AnimeEpisodeID, AnimeSeriesID)
            {
                WatchedDate = GetVideoLocals()
                    .Select(file => file.GetUserRecord(userID))
                    .Where(record => record != null && record.WatchedDate.HasValue)
                    .OrderBy(record => record.WatchedDate)
                    .FirstOrDefault()?.WatchedDate,
            };
            if (session != null)
                RepoFactory.AnimeEpisode_User.SaveWithOpenTransaction(session, userRecord);
            else
                RepoFactory.AnimeEpisode_User.Save(userRecord);
            return userRecord;
        }


        /// <summary>
        /// Gets the AnimeSeries this episode belongs to
        /// </summary>
        public SVR_AnimeSeries GetAnimeSeries()
        {
            return RepoFactory.AnimeSeries.GetByID(AnimeSeriesID);
        }

        public List<SVR_VideoLocal> GetVideoLocals()
        {
            return RepoFactory.VideoLocal.GetByAniDBEpisodeID(AniDB_EpisodeID);
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

        public CL_AnimeEpisode_User GetUserContract(int userID, ISessionWrapper session = null)
        {
            var anidbEpisode = AniDB_Episode;
            var seriesUserRecord = RepoFactory.AnimeSeries_User.GetByUserAndSeriesID(userID, AnimeSeriesID);
            var episodeUserRecord = GetOrCreateUserRecord(userID, session);
            var contract = new CL_AnimeEpisode_User
            {
                AniDB_EpisodeID = AniDB_EpisodeID,
                AnimeEpisodeID = AnimeEpisodeID,
                AnimeSeriesID = AnimeSeriesID,
                DateTimeCreated = DateTimeCreated,
                DateTimeUpdated = DateTimeUpdated,
                PlayedCount = episodeUserRecord.PlayedCount,
                StoppedCount = episodeUserRecord.StoppedCount,
                WatchedCount = episodeUserRecord.WatchedCount,
                WatchedDate = episodeUserRecord.WatchedDate,
                AniDB_EnglishName = RepoFactory.AniDB_Episode_Title.GetByEpisodeIDAndLanguage(AniDB_EpisodeID, "EN")
                    .FirstOrDefault()?.Title,
                AniDB_RomajiName = RepoFactory.AniDB_Episode_Title.GetByEpisodeIDAndLanguage(AniDB_EpisodeID, "X-JAT")
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
            GetVideoLocals().SelectMany(a => a.Places).ForEach(place =>
            {
                if (!deleteFiles) place.RemoveRecord();
                else place.RemoveRecordAndDeletePhysicalFile(false);
            }, place =>
            {
                if (!deleteFiles) place.RemoveRecord();
                else place.RemoveRecordAndDeletePhysicalFile();
            });
        }

        public string Title
        {
            get
            {
                var languages = Languages.PreferredEpisodeNamingLanguages.Select(a => a.Language);
                foreach (var language in languages)
                {
                    var episode_title =
                        RepoFactory.AniDB_Episode_Title.GetByEpisodeIDAndLanguage(AniDB_EpisodeID, language);
                    var title = episode_title.FirstOrDefault();
                    if (string.IsNullOrEmpty(title?.Title)) continue;
                    return title?.Title;
                }

                return RepoFactory.AniDB_Episode_Title.GetByEpisodeIDAndLanguage(AniDB_EpisodeID, "EN").FirstOrDefault()
                    ?.Title;
            }
        }

        IReadOnlyList<AnimeTitle> IEpisode.Titles =>
            RepoFactory.AniDB_Episode_Title.GetByEpisodeID(AniDB_EpisodeID)
                .Select(a => new AnimeTitle { LanguageCode = a.Language, Language = a.Language.GetEnum(), Title = a.Title })
                .ToList();
        int IEpisode.EpisodeID => AniDB_EpisodeID;
        int IEpisode.AnimeID => AniDB_Episode?.AnimeID ?? 0;
        int IEpisode.Duration => AniDB_Episode?.LengthSeconds ?? 0;
        int IEpisode.Number => AniDB_Episode?.EpisodeNumber ?? 0;
        Shoko.Plugin.Abstractions.DataModels.EpisodeType IEpisode.Type =>
            (Shoko.Plugin.Abstractions.DataModels.EpisodeType) (AniDB_Episode?.EpisodeType ?? 0);
        DateTime? IEpisode.AirDate => AniDB_Episode?.GetAirDateAsDate();
    }
}

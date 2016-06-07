using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using AniDBAPI;
using JMMContracts;
using JMMContracts.KodiContracts;
using JMMServer.Entities;
using JMMServer.Kodi;
using JMMServer.Plex;
using JMMServer.Repositories;
using NHibernate;
using NLog;

namespace JMMServer
{
    public class StatsCache
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private static StatsCache _instance;

        public Dictionary<int, Contract_AniDB_AnimeDetailed> StatAnimeContracts;
        // AnimeID / Contract_AniDB_AnimeDetailed

        public Dictionary<int, DateTime?> StatGroupAirDate_Max; // AnimeGroupID / AirDate_Max 
        public Dictionary<int, DateTime?> StatGroupAirDate_Min; // AnimeGroupID / AirDate_Min
        public Dictionary<int, decimal> StatGroupAniDBRating; // AnimeGroupID / AniDBVote
        public Dictionary<int, string> StatGroupAudioLanguages; // AnimeGroupID / audio language List
        public Dictionary<int, string> StatGroupCustomTags; // AnimeGroupID / Custom Tags
        public Dictionary<int, DateTime?> StatGroupEndDate; // AnimeGroupID / EndDate
        public Dictionary<int, int> StatGroupEpisodeCount; // AnimeGroupID
        public Dictionary<int, bool> StatGroupHasMAL; // AnimeGroupID
        public Dictionary<int, bool> StatGroupHasMovieDB; // AnimeGroupID
        public Dictionary<int, bool> StatGroupHasMovieDBOrTvDB; // AnimeGroupID
        public Dictionary<int, bool> StatGroupHasTvDB; // AnimeGroupID
        public Dictionary<int, bool> StatGroupIsComplete; // AnimeGroupID
        public Dictionary<int, bool> StatGroupIsCurrentlyAiring; // AnimeGroupID
        public Dictionary<int, bool> StatGroupIsFinishedAiring; // AnimeGroupID
        public Dictionary<int, int> StatGroupSeriesCount; // AnimeGroupID
        public Dictionary<int, DateTime?> StatGroupSeriesCreatedDate; // AnimeGroupID / SeriesCreatedDate
        public Dictionary<int, string> StatGroupSubtitleLanguages; // AnimeGroupID / subtitle language List

        public Dictionary<int, string> StatGroupTags; // AnimeGroupID / Categories List
        public Dictionary<int, string> StatGroupTitles; // AnimeGroupID / Titles List
        public Dictionary<int, decimal?> StatGroupUserVoteOverall; // AnimeGroupID / UserVoteOverall
        public Dictionary<int, decimal?> StatGroupUserVotePermanent; // AnimeGroupID / UserVotePermanent
        public Dictionary<int, decimal?> StatGroupUserVoteTemporary; // AnimeGroupID / UserVoteTemporary
        public Dictionary<int, string> StatGroupVideoQuality; // AnimeGroupID / Video Quality List
        public Dictionary<int, string> StatGroupVideoQualityEpisodes; // AnimeGroupID / Video Quality List
        public Dictionary<int, Dictionary<int, Video>> StatKodiGroupsCache;

        public Dictionary<int, Dictionary<int, JMMContracts.PlexContracts.Video>> StatPlexGroupsCache;

        public Dictionary<int, Dictionary<int, HashSet<int>>> StatUserGroupFilter;


        public StatsCache()
        {
            ClearAllData();
        }

        public static StatsCache Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new StatsCache();
                }
                return _instance;
            }
        }

        private void ClearAllData()
        {
            StatGroupTags = new Dictionary<int, string>();
            StatGroupCustomTags = new Dictionary<int, string>();
            StatGroupTitles = new Dictionary<int, string>();
            StatGroupAirDate_Min = new Dictionary<int, DateTime?>();
            StatGroupAirDate_Max = new Dictionary<int, DateTime?>();
            StatGroupEndDate = new Dictionary<int, DateTime?>();
            StatGroupSeriesCreatedDate = new Dictionary<int, DateTime?>();
            StatGroupUserVoteOverall = new Dictionary<int, decimal?>();
            StatGroupUserVotePermanent = new Dictionary<int, decimal?>();
            StatGroupUserVoteTemporary = new Dictionary<int, decimal?>();
            StatGroupIsComplete = new Dictionary<int, bool>();
            StatGroupIsFinishedAiring = new Dictionary<int, bool>();
            StatGroupIsCurrentlyAiring = new Dictionary<int, bool>();
            StatGroupVideoQuality = new Dictionary<int, string>();
            StatGroupVideoQualityEpisodes = new Dictionary<int, string>();
            StatGroupAudioLanguages = new Dictionary<int, string>();
            StatGroupSubtitleLanguages = new Dictionary<int, string>();
            StatGroupHasTvDB = new Dictionary<int, bool>();
            StatGroupHasMAL = new Dictionary<int, bool>();
            StatGroupHasMovieDB = new Dictionary<int, bool>();
            StatGroupHasMovieDBOrTvDB = new Dictionary<int, bool>();
            StatGroupSeriesCount = new Dictionary<int, int>();
            StatGroupEpisodeCount = new Dictionary<int, int>();
            StatGroupAniDBRating = new Dictionary<int, decimal>();
            StatAnimeContracts = new Dictionary<int, Contract_AniDB_AnimeDetailed>();

            StatUserGroupFilter = new Dictionary<int, Dictionary<int, HashSet<int>>>();

            StatPlexGroupsCache = new Dictionary<int, Dictionary<int, JMMContracts.PlexContracts.Video>>();
            StatKodiGroupsCache = new Dictionary<int, Dictionary<int, Video>>();
        }

        public void UpdatePlexAnimeGroup(ISession session, AnimeGroup grp, List<AnimeSeries> allSeries)
        {
            if (!ServerSettings.EnablePlex) return;

            var repUser = new JMMUserRepository();
            var repUserGroups = new AnimeGroup_UserRepository();
            foreach (var user in repUser.GetAll(session))
            {
                var userRec = repUserGroups.GetByUserAndGroupID(session, user.JMMUserID, grp.AnimeGroupID);
                Dictionary<int, JMMContracts.PlexContracts.Video> cdic;
                if (StatPlexGroupsCache.ContainsKey(user.JMMUserID))
                    cdic = StatPlexGroupsCache[user.JMMUserID];
                else
                {
                    cdic = new Dictionary<int, JMMContracts.PlexContracts.Video>();
                    StatPlexGroupsCache[user.JMMUserID] = cdic;
                }
                cdic[grp.AnimeGroupID] = PlexHelper.VideoFromAnimeGroup(session, grp, user.JMMUserID, allSeries);
            }
        }

        public void UpdateKodiAnimeGroup(ISession session, AnimeGroup grp, List<AnimeSeries> allSeries)
        {
            if (!ServerSettings.EnableKodi) return;

            var repUser = new JMMUserRepository();
            var repUserGroups = new AnimeGroup_UserRepository();
            foreach (var user in repUser.GetAll(session))
            {
                var userRec = repUserGroups.GetByUserAndGroupID(session, user.JMMUserID, grp.AnimeGroupID);
                Dictionary<int, Video> cdic;
                if (StatKodiGroupsCache.ContainsKey(user.JMMUserID))
                    cdic = StatKodiGroupsCache[user.JMMUserID];
                else
                {
                    cdic = new Dictionary<int, Video>();
                    StatKodiGroupsCache[user.JMMUserID] = cdic;
                }
                cdic[grp.AnimeGroupID] = KodiHelper.VideoFromAnimeGroup(session, grp, user.JMMUserID, allSeries);
            }
        }


        public void UpdateGroupFilterUsingGroupFilter(int groupfilter)
        {
            var repGroups = new AnimeGroupRepository();
            var repUserGroups = new AnimeGroup_UserRepository();
            var repUser = new JMMUserRepository();
            var repGrpFilter = new GroupFilterRepository();
            var gf = repGrpFilter.GetByID(groupfilter);
            if (gf == null)
                return;

            foreach (var user in repUser.GetAll())
            {
                Dictionary<int, HashSet<int>> groupfilters;
                if (StatUserGroupFilter.ContainsKey(user.JMMUserID))
                    groupfilters = StatUserGroupFilter[user.JMMUserID];
                else
                {
                    groupfilters = new Dictionary<int, HashSet<int>>();
                    StatUserGroupFilter.Add(user.JMMUserID, groupfilters);
                }
                HashSet<int> groups;
                if (groupfilters.ContainsKey(groupfilter))
                    groups = groupfilters[groupfilter];
                else
                {
                    groups = new HashSet<int>();
                    groupfilters.Add(groupfilter, groups);
                }
                groups.Clear();
                var allGrps = repGroups.GetAllTopLevelGroups(); // No Need of subgroups

                foreach (var grp in allGrps)
                {
                    var userRec = repUserGroups.GetByUserAndGroupID(user.JMMUserID, grp.AnimeGroupID);
                    if (EvaluateGroupFilter(gf, grp, user, userRec))
                        groups.Add(grp.AnimeGroupID);
                }
            }
        }

        public void UpdateGroupFilterUsingUser(int userid)
        {
            var repGroups = new AnimeGroupRepository();
            var repUserGroups = new AnimeGroup_UserRepository();
            var repUser = new JMMUserRepository();
            var repGrpFilter = new GroupFilterRepository();
            var user = repUser.GetByID(userid);
            if (user == null)
                return;
            Dictionary<int, HashSet<int>> groupfilters;
            if (StatUserGroupFilter.ContainsKey(user.JMMUserID))
                groupfilters = StatUserGroupFilter[user.JMMUserID];
            else
            {
                groupfilters = new Dictionary<int, HashSet<int>>();
                StatUserGroupFilter.Add(user.JMMUserID, groupfilters);
            }
            var gfs = repGrpFilter.GetAll();
            var gfgf = new GroupFilter();
            gfgf.GroupFilterName = "All";
            gfs.Add(gfgf);
            foreach (var gf in gfs)
            {
                HashSet<int> groups;
                if (groupfilters.ContainsKey(gf.GroupFilterID))
                    groups = groupfilters[gf.GroupFilterID];
                else
                {
                    groups = new HashSet<int>();
                    groupfilters.Add(gf.GroupFilterID, groups);
                }
                groups.Clear();
                var allGrps = repGroups.GetAllTopLevelGroups(); // No Need of subgroups

                foreach (var grp in allGrps)
                {
                    var userRec = repUserGroups.GetByUserAndGroupID(user.JMMUserID, grp.AnimeGroupID);
                    if (EvaluateGroupFilter(gf, grp, user, userRec))
                        groups.Add(grp.AnimeGroupID);
                }
            }
        }


        public void UpdateGroupFilterUsingGroup(int groupid)
        {
            var repGroups = new AnimeGroupRepository();
            var repUserGroups = new AnimeGroup_UserRepository();
            var repUser = new JMMUserRepository();
            var repGrpFilter = new GroupFilterRepository();

            var grp = repGroups.GetByID(groupid);
            if (grp.AnimeGroupParentID.HasValue)
                return;
            foreach (var user in repUser.GetAll())
            {
                var userRec = repUserGroups.GetByUserAndGroupID(user.JMMUserID, groupid);

                Dictionary<int, HashSet<int>> groupfilters;
                if (StatUserGroupFilter.ContainsKey(user.JMMUserID))
                    groupfilters = StatUserGroupFilter[user.JMMUserID];
                else
                {
                    groupfilters = new Dictionary<int, HashSet<int>>();
                    StatUserGroupFilter.Add(user.JMMUserID, groupfilters);
                }
                var gfs = repGrpFilter.GetAll();
                var gfgf = new GroupFilter();
                gfgf.GroupFilterName = "All";
                gfs.Add(gfgf);

                foreach (var gf in gfs)
                {
                    HashSet<int> groups;
                    if (groupfilters.ContainsKey(gf.GroupFilterID))
                        groups = groupfilters[gf.GroupFilterID];
                    else
                    {
                        groups = new HashSet<int>();
                        groupfilters.Add(gf.GroupFilterID, groups);
                    }
                    if (groups.Contains(groupid))
                        groups.Remove(groupid);
                    if (EvaluateGroupFilter(gf, grp, user, userRec))
                        groups.Add(grp.AnimeGroupID);
                }
            }
        }

        public void UpdateUsingAniDBFile(string hash)
        {
            try
            {
                var start = DateTime.Now;
                var repAniFile = new AniDB_FileRepository();
                var anifile = repAniFile.GetByHash(hash);
                if (anifile == null) return;

                UpdateUsingAnime(anifile.AnimeID);
                UpdateAnimeContract(anifile.AnimeID);

                var ts = DateTime.Now - start;
                logger.Info("Updated cached stats file ({0}) in {1} ms", hash, ts.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
        }

        public void UpdateUsingAnime(int animeID)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                UpdateUsingAnime(session, animeID);
            }
        }

        public void UpdateUsingAnime(ISession session, int animeID)
        {
            try
            {
                var repAnime = new AniDB_AnimeRepository();
                var anime = repAnime.GetByAnimeID(session, animeID);
                if (anime == null) return;

                var repSeries = new AnimeSeriesRepository();
                var ser = repSeries.GetByAnimeID(session, animeID);
                if (ser == null) return;

                UpdateUsingSeries(session, ser.AnimeSeriesID);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
        }

        public void UpdateAnimeContract(ISession session, int animeID)
        {
            try
            {
                var repAnime = new AniDB_AnimeRepository();
                var anime = repAnime.GetByAnimeID(session, animeID);
                if (anime == null) return;

                StatAnimeContracts[anime.AnimeID] = anime.ToContractDetailed(session);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
        }

        public void UpdateAnimeContract(int animeID)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                UpdateAnimeContract(session, animeID);
            }
        }

        /*
		public void UpdateAllAnimeContracts()
		{
			try
			{
				DateTime start = DateTime.Now;

				AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
				using (var session = JMMService.SessionFactory.OpenSession())
				{
					var series = session
						.CreateCriteria(typeof(AnimeSeries))
						.List<AnimeSeries>();

                    List<AnimeSeries> allSeries = new List<AnimeSeries>(series);
					foreach (AnimeSeries ser in allSeries)
					{
						UpdateAnimeContract(session, ser.AniDB_ID);
					}

					TimeSpan ts = DateTime.Now - start;
					logger.Info("Updated All Anime Contracts: {0} in {1} ms", allSeries.Count, ts.TotalMilliseconds);
				}

				
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
			}

		}
        */

        /// <summary>
        ///     Use whenever a series is added to or removed from a group
        /// </summary>
        /// <param name="animeSeriesID"></param>
        public void UpdateUsingSeries(int animeSeriesID)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                UpdateUsingSeries(session, animeSeriesID);
            }
        }

        public void UpdateUsingSeries(ISession session, int animeSeriesID)
        {
            try
            {
                var start = DateTime.Now;

                var repSeries = new AnimeSeriesRepository();
                var ser = repSeries.GetByID(session, animeSeriesID);
                if (ser == null) return;

                foreach (var grp in ser.AllGroupsAbove)
                {
                    UpdateUsingGroup(grp.AnimeGroupID);
                }

                var ts = DateTime.Now - start;
                logger.Info("Updated cached stats series ({0}) in {1} ms", ser.GetAnime().MainTitle,
                    ts.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
        }

        /*
        public const int UpdatetimeInSeconds = 90;

        private static TimeUpdater<int, object> updates = new TimeUpdater<int, object>(UpdatetimeInSeconds, "UpdateGroupsStats", InternalUpdaterAction);



        private static void InternalUpdaterAction(int groupid, object obj)
        {
            StatsCache.Instance.InternalUpdateUsingGroup(groupid);
        }

	    public void UpdateUsingGroup(int animeGroupID)
	    {
            updates.Update(animeGroupID,null);
	    }
        */

        public void UpdateUsingGroup(int animeGroupID)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                UpdateUsingGroup(session, animeGroupID);
            }
        }


        public void UpdateUsingGroup(ISession session, int animeGroupID)
        {
            try
            {
                var start = DateTime.Now;

                var repGroups = new AnimeGroupRepository();
                var thisgrp = repGroups.GetByID(session, animeGroupID);

                if (thisgrp == null) return;

                var repAdHoc = new AdhocRepository();

                // get a list of all the groups including this one and everthing above it the heirarchy
                var allgroups = new List<AnimeGroup>();
                allgroups.Add(thisgrp);

                var groupID = thisgrp.AnimeGroupParentID;
                while (groupID.HasValue)
                {
                    var grpTemp = repGroups.GetByID(session, groupID.Value);
                    if (grpTemp != null)
                    {
                        allgroups.Add(grpTemp);
                        groupID = grpTemp.AnimeGroupParentID;
                    }
                    else
                        groupID = null;
                }

                var ts = DateTime.Now - start;
                logger.Trace("Updating cached stats for GROUP - STEP 1 ({0}) in {1} ms", thisgrp.GroupName,
                    ts.TotalMilliseconds);
                start = DateTime.Now;

                var repVids = new VideoLocalRepository();
                var repXrefs = new CrossRef_File_EpisodeRepository();

                foreach (var grp in allgroups)
                {
                    StatGroupTags[grp.AnimeGroupID] = grp.TagsString;
                    StatGroupCustomTags[grp.AnimeGroupID] = grp.CustomTagsString;
                    StatGroupTitles[grp.AnimeGroupID] = grp.TitlesString;
                    StatGroupVideoQuality[grp.AnimeGroupID] = grp.VideoQualityString;

                    ts = DateTime.Now - start;
                    logger.Trace("Updating cached stats for GROUP - STEP 2 ({0}) in {1} ms", grp.GroupName,
                        ts.TotalMilliseconds);
                    start = DateTime.Now;

                    DateTime? airDate_Min = null;
                    DateTime? airDate_Max = null;
                    DateTime? endDate = new DateTime(1980, 1, 1);
                    DateTime? seriesCreatedDate = null;
                    var isComplete = false;
                    var hasFinishedAiring = false;
                    var isCurrentlyAiring = false;
                    var videoQualityEpisodes = "";

                    var audioLanguages = new List<string>();
                    var subtitleLanguages = new List<string>();

                    var hasTvDB = true;
                    var hasMAL = true;
                    var hasMovieDB = true;
                    var hasMovieDBOrTvDB = true;

                    var seriesCount = 0;
                    var epCount = 0;

                    foreach (var series in grp.GetAllSeries(session))
                    {
                        seriesCount++;

                        var anime = series.GetAnime(session);

                        var vidsTemp = repVids.GetByAniDBAnimeID(session, series.AniDB_ID);
                        var crossRefs = repXrefs.GetByAnimeID(session, series.AniDB_ID);

                        var dictCrossRefs = new Dictionary<int, List<CrossRef_File_Episode>>();
                        foreach (var xref in crossRefs)
                        {
                            if (!dictCrossRefs.ContainsKey(xref.EpisodeID))
                                dictCrossRefs[xref.EpisodeID] = new List<CrossRef_File_Episode>();
                            dictCrossRefs[xref.EpisodeID].Add(xref);
                        }

                        var dictVids = new Dictionary<string, VideoLocal>();
                        foreach (var vid in vidsTemp)
                            dictVids[vid.Hash] = vid;

                        // All Video Quality Episodes
                        // Try to determine if this anime has all the episodes available at a certain video quality
                        // e.g.  the series has all episodes in blu-ray
                        // Also look at languages
                        var vidQualEpCounts = new Dictionary<string, int>(); // video quality, count of episodes

                        foreach (var ep in series.GetAnimeEpisodes(session))
                        {
                            if (ep.EpisodeTypeEnum != enEpisodeType.Episode) continue;

                            if ((!anime.LatestEpisodeNumber.HasValue ||
                                 anime.LatestEpisodeNumber.Value < ep.AniDB_Episode.EpisodeNumber) &&
                                ep.AniDB_Episode.AirDateAsDate <= DateTime.Now)
                            {
                                anime.LatestEpisodeNumber = ep.AniDB_Episode.EpisodeNumber;
                                anime.LatestEpisodeAirDate = ep.AniDB_Episode.AirDateAsDate;
                            }

                            var epVids = new List<VideoLocal>();
                            if (dictCrossRefs.ContainsKey(ep.AniDB_EpisodeID))
                            {
                                foreach (var xref in dictCrossRefs[ep.AniDB_EpisodeID])
                                {
                                    if (xref.EpisodeID == ep.AniDB_EpisodeID)
                                    {
                                        if (dictVids.ContainsKey(xref.Hash))
                                            epVids.Add(dictVids[xref.Hash]);
                                    }
                                }
                            }

                            var qualityAddedSoFar = new List<string>();
                            // handle mutliple files of the same quality for one episode
                            foreach (var vid in epVids)
                            {
                                var anifile = vid.GetAniDBFile(session);
                                if (anifile == null) continue;

                                if (!qualityAddedSoFar.Contains(anifile.File_Source))
                                {
                                    if (!vidQualEpCounts.ContainsKey(anifile.File_Source))
                                        vidQualEpCounts[anifile.File_Source] = 1;
                                    else
                                        vidQualEpCounts[anifile.File_Source]++;

                                    qualityAddedSoFar.Add(anifile.File_Source);
                                }
                            }
                        }

                        using (var transaction = session.BeginTransaction())
                        {
                            session.SaveOrUpdate(anime);
                            transaction.Commit();
                        }

                        ts = DateTime.Now - start;
                        logger.Trace("Updating cached stats for GROUP/Series - STEP 3 ({0}/{1}) in {2} ms",
                            grp.GroupName, series.AnimeSeriesID, ts.TotalMilliseconds);
                        start = DateTime.Now;

                        var time = anime.LatestEpisodeAirDate;
                        if (time.HasValue)
                        {
                            if (grp.LatestEpisodeAirDate.HasValue)
                            {
                                if (grp.LatestEpisodeAirDate.Value < time)
                                {
                                    grp.LatestEpisodeAirDate = time;
                                }
                            }
                            else
                            {
                                grp.LatestEpisodeAirDate = time;
                            }
                            using (var transaction = session.BeginTransaction())
                            {
                                session.SaveOrUpdate(grp);
                                transaction.Commit();
                            }

                            logger.Trace("Updating Latest Episode for: {0}", grp.AnimeGroupID);
                        }

                        epCount = epCount + anime.EpisodeCountNormal;

                        foreach (var kvp in vidQualEpCounts)
                        {
                            var index = videoQualityEpisodes.IndexOf(kvp.Key, 0,
                                StringComparison.InvariantCultureIgnoreCase);
                            if (index > -1) continue; // don't add if we already have it

                            if (anime.EpisodeCountNormal == kvp.Value)
                            {
                                if (videoQualityEpisodes.Length > 0) videoQualityEpisodes += ",";
                                videoQualityEpisodes += kvp.Key;
                            }
                        }

                        ts = DateTime.Now - start;
                        logger.Trace("Updating cached stats for GROUP/Series - STEP 4 ({0}/{1}) in {2} ms",
                            grp.GroupName, series.AnimeSeriesID, ts.TotalMilliseconds);
                        start = DateTime.Now;

                        // audio languages
                        var dicAudio = repAdHoc.GetAudioLanguageStatsByAnime(session, anime.AnimeID);
                        foreach (var kvp in dicAudio)
                        {
                            foreach (var lanName in kvp.Value.LanguageNames)
                            {
                                if (!audioLanguages.Contains(lanName))
                                    audioLanguages.Add(lanName);
                            }
                        }

                        ts = DateTime.Now - start;
                        logger.Trace("Updating cached stats for GROUP/Series - STEP 5 ({0}/{1}) in {2} ms",
                            grp.GroupName, series.AnimeSeriesID, ts.TotalMilliseconds);
                        start = DateTime.Now;

                        // subtitle languages
                        var dicSubtitle = repAdHoc.GetSubtitleLanguageStatsByAnime(session, anime.AnimeID);
                        foreach (var kvp in dicSubtitle)
                        {
                            foreach (var lanName in kvp.Value.LanguageNames)
                            {
                                if (!subtitleLanguages.Contains(lanName))
                                    subtitleLanguages.Add(lanName);
                            }
                        }

                        ts = DateTime.Now - start;
                        logger.Trace("Updating cached stats for GROUP/Series - STEP 6 ({0}/{1}) in {2} ms",
                            grp.GroupName, series.AnimeSeriesID, ts.TotalMilliseconds);
                        start = DateTime.Now;

                        // Calculate Air Date 
                        var thisDate = series.AirDate;
                        if (thisDate.HasValue)
                        {
                            if (airDate_Min.HasValue)
                            {
                                if (thisDate.Value < airDate_Min.Value) airDate_Min = thisDate;
                            }
                            else
                                airDate_Min = thisDate;

                            if (airDate_Max.HasValue)
                            {
                                if (thisDate.Value > airDate_Max.Value) airDate_Max = thisDate;
                            }
                            else
                                airDate_Max = thisDate;
                        }

                        // calculate end date
                        // if the end date is NULL it actually means it is ongoing, so this is the max possible value
                        thisDate = series.EndDate;
                        if (thisDate.HasValue && endDate.HasValue)
                        {
                            if (thisDate.Value > endDate.Value) endDate = thisDate;
                        }
                        else
                            endDate = null;

                        // Note - only one series has to be finished airing to qualify
                        if (series.EndDate.HasValue && series.EndDate.Value < DateTime.Now)
                            hasFinishedAiring = true;

                        // Note - only one series has to be finished airing to qualify
                        if (!series.EndDate.HasValue || series.EndDate.Value > DateTime.Now)
                            isCurrentlyAiring = true;

                        // We evaluate IsComplete as true if
                        // 1. series has finished airing
                        // 2. user has all episodes locally
                        // Note - only one series has to be complete for the group to be considered complete
                        if (series.EndDate.HasValue)
                        {
                            if (series.EndDate.Value < DateTime.Now && series.MissingEpisodeCount == 0 &&
                                series.MissingEpisodeCountGroups == 0)
                            {
                                isComplete = true;
                            }
                        }

                        // Calculate Series Created Date 
                        thisDate = series.DateTimeCreated;
                        if (thisDate.HasValue)
                        {
                            if (seriesCreatedDate.HasValue)
                            {
                                if (thisDate.Value < seriesCreatedDate.Value) seriesCreatedDate = thisDate;
                            }
                            else
                                seriesCreatedDate = thisDate;
                        }

                        ts = DateTime.Now - start;
                        logger.Trace("Updating cached stats for GROUP/Series - STEP 7 ({0}/{1}) in {2} ms",
                            grp.GroupName, series.AnimeSeriesID, ts.TotalMilliseconds);
                        start = DateTime.Now;

                        // for the group, if any of the series don't have a tvdb link
                        // we will consider the group as not having a tvdb link

                        var tvXrefs = series.GetCrossRefTvDBV2();

                        if (tvXrefs == null || tvXrefs.Count == 0) hasTvDB = false;
                        if (series.CrossRefMovieDB == null) hasMovieDB = false;
                        if (series.CrossRefMAL == null) hasMAL = false;

                        if ((tvXrefs == null || tvXrefs.Count == 0) && series.CrossRefMovieDB == null)
                            hasMovieDBOrTvDB = false;
                    }


                    StatGroupIsComplete[grp.AnimeGroupID] = isComplete;
                    StatGroupIsFinishedAiring[grp.AnimeGroupID] = hasFinishedAiring;
                    StatGroupIsCurrentlyAiring[grp.AnimeGroupID] = isCurrentlyAiring;
                    StatGroupHasTvDB[grp.AnimeGroupID] = hasTvDB;
                    StatGroupHasMAL[grp.AnimeGroupID] = hasMAL;
                    StatGroupHasMovieDB[grp.AnimeGroupID] = hasMovieDB;
                    StatGroupHasMovieDBOrTvDB[grp.AnimeGroupID] = hasMovieDBOrTvDB;
                    StatGroupSeriesCount[grp.AnimeGroupID] = seriesCount;
                    StatGroupEpisodeCount[grp.AnimeGroupID] = epCount;

                    StatGroupVideoQualityEpisodes[grp.AnimeGroupID] = videoQualityEpisodes;

                    StatGroupAirDate_Min[grp.AnimeGroupID] = airDate_Min;
                    StatGroupAirDate_Max[grp.AnimeGroupID] = airDate_Max;
                    StatGroupEndDate[grp.AnimeGroupID] = endDate;
                    StatGroupSeriesCreatedDate[grp.AnimeGroupID] = seriesCreatedDate;

                    StatGroupUserVoteOverall[grp.AnimeGroupID] = grp.UserVote;
                    StatGroupUserVotePermanent[grp.AnimeGroupID] = grp.UserVotePermanent;
                    StatGroupUserVoteTemporary[grp.AnimeGroupID] = grp.UserVoteTemporary;
                    StatGroupAniDBRating[grp.AnimeGroupID] = grp.AniDBRating;

                    ts = DateTime.Now - start;
                    logger.Trace("Updating cached stats for GROUP - STEP 8 ({0}) in {1} ms", grp.GroupName,
                        ts.TotalMilliseconds);
                    start = DateTime.Now;

                    var Stat_AudioLanguages = "";
                    foreach (var audioLan in audioLanguages)
                    {
                        if (Stat_AudioLanguages.Length > 0) Stat_AudioLanguages += ",";
                        Stat_AudioLanguages += audioLan;
                    }
                    StatGroupAudioLanguages[grp.AnimeGroupID] = Stat_AudioLanguages;

                    var Stat_SubtitleLanguages = "";
                    foreach (var subLan in subtitleLanguages)
                    {
                        if (Stat_SubtitleLanguages.Length > 0) Stat_SubtitleLanguages += ",";
                        Stat_SubtitleLanguages += subLan;
                    }
                    StatGroupSubtitleLanguages[grp.AnimeGroupID] = Stat_SubtitleLanguages;

                    ts = DateTime.Now - start;
                    logger.Trace("Updating cached stats for GROUP - STEP 9 ({0}) in {1} ms", grp.GroupName,
                        ts.TotalMilliseconds);
                    start = DateTime.Now;
                    UpdateGroupFilterUsingGroup(grp.AnimeGroupID);
                    UpdatePlexAnimeGroup(session, grp, grp.GetAllSeries());
                    UpdateKodiAnimeGroup(session, grp, grp.GetAllSeries());
                    ts = DateTime.Now - start;
                    logger.Trace("Updating cached stats for GROUP - END ({0}) in {1} ms", grp.GroupName,
                        ts.TotalMilliseconds);
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
        }


        public void InitStats()
        {
            try
            {
                var start = DateTime.Now;

                ClearAllData();

                #region Get the data

                var repGroups = new AnimeGroupRepository();
                var repAnime = new AniDB_AnimeRepository();
                var repSeries = new AnimeSeriesRepository();
                var repTags = new AniDB_TagRepository();
                var repAnimeTag = new AniDB_Anime_TagRepository();
                var repTitles = new AniDB_Anime_TitleRepository();

                var allGrps = repGroups.GetAll();

                var allGroupsDict = new Dictionary<int, AnimeGroup>();
                foreach (var agrp in allGrps)
                    allGroupsDict[agrp.AnimeGroupID] = agrp;
                var ts = DateTime.Now - start;
                logger.Info("Get All GROUPS (Database) in {0} ms", ts.TotalMilliseconds);


                // anime
                start = DateTime.Now;
                var allAnime = repAnime.GetAll();
                var allAnimeDict = new Dictionary<int, AniDB_Anime>();
                foreach (var anime in allAnime)
                    allAnimeDict[anime.AnimeID] = anime;

                ts = DateTime.Now - start;
                logger.Info("Get All ANIME (Database) in {0} ms", ts.TotalMilliseconds);

                // tags
                start = DateTime.Now;
                var allTags = repTags.GetAll();
                var allTagsDict = new Dictionary<int, AniDB_Tag>();
                foreach (var tag in allTags)
                    allTagsDict[tag.TagID] = tag;


                var allAnimeTags = repAnimeTag.GetAll();
                var allAnimeTagsDict = new Dictionary<int, List<int>>(); // animeid / list of tag id's
                foreach (var aniTag in allAnimeTags)
                {
                    if (!allAnimeTagsDict.ContainsKey(aniTag.AnimeID))
                        allAnimeTagsDict[aniTag.AnimeID] = new List<int>();

                    allAnimeTagsDict[aniTag.AnimeID].Add(aniTag.TagID);
                }
                ts = DateTime.Now - start;
                logger.Info("Get All TAGS (Database) in {0} ms", ts.TotalMilliseconds);


                // custom tags
                var repCustomTags = new CustomTagRepository();
                var repXRefCustomTags = new CrossRef_CustomTagRepository();

                var allCustomTags = repCustomTags.GetAll();
                var allCustomTagsDict = new Dictionary<int, CustomTag>();
                foreach (var tag in allCustomTags)
                    allCustomTagsDict[tag.CustomTagID] = tag;

                var allCustomTagsXRefs = repXRefCustomTags.GetAll();
                var allCustomTagsXRefDict = new Dictionary<int, List<CrossRef_CustomTag>>(); // 
                foreach (var aniTag in allCustomTagsXRefs)
                {
                    if (!allCustomTagsXRefDict.ContainsKey(aniTag.CrossRefID))
                        allCustomTagsXRefDict[aniTag.CrossRefID] = new List<CrossRef_CustomTag>();

                    allCustomTagsXRefDict[aniTag.CrossRefID].Add(aniTag);
                }


                // titles
                start = DateTime.Now;
                var allTitles = repTitles.GetAll();
                var allTitlesDict = new Dictionary<int, List<AniDB_Anime_Title>>(); // animeid / list of titles
                foreach (var aniTitle in allTitles)
                {
                    if (!allTitlesDict.ContainsKey(aniTitle.AnimeID))
                        allTitlesDict[aniTitle.AnimeID] = new List<AniDB_Anime_Title>();

                    allTitlesDict[aniTitle.AnimeID].Add(aniTitle);
                }
                ts = DateTime.Now - start;
                logger.Info("Get All TITLES (Database) in {0} ms", ts.TotalMilliseconds);

                // user votes
                start = DateTime.Now;
                var repVotes = new AniDB_VoteRepository();
                var allVotes = repVotes.GetAll();
                ts = DateTime.Now - start;
                logger.Info("Get All VOTES (Database) in {0} ms", ts.TotalMilliseconds);

                // video quality
                start = DateTime.Now;
                var rep = new AdhocRepository();
                var allVidQuality = rep.GetAllVideoQualityByGroup();

                ts = DateTime.Now - start;
                logger.Info("Get VIDEO QUALITY STATS (Database) in {0} ms", ts.TotalMilliseconds);

                // video quality episode stats
                start = DateTime.Now;
                var dictStats = rep.GetEpisodeVideoQualityStatsByAnime();
                ts = DateTime.Now - start;
                logger.Info("Get VIDEO QUALITY EPISODE STATS (Database) in {0} ms", ts.TotalMilliseconds);

                // audio and subtitle language stats
                start = DateTime.Now;
                var dictAudioStats = rep.GetAudioLanguageStatsForAnime();
                var dictSubtitleStats = rep.GetSubtitleLanguageStatsForAnime();
                ts = DateTime.Now - start;
                logger.Info("Get LANGUAGE STATS (Database) in {0} ms", ts.TotalMilliseconds);

                start = DateTime.Now;
                var allSeries = repSeries.GetAll();
                ts = DateTime.Now - start;
                logger.Info("Get All Series (Database) in {0} ms", ts.TotalMilliseconds);

                // TvDB
                start = DateTime.Now;
                var repCrossRef = new CrossRef_AniDB_TvDBV2Repository();
                var allCrossRefs = repCrossRef.GetAll();
                var animeWithTvDBCrossRef = new List<int>();
                foreach (var xref in allCrossRefs)
                {
                    if (!animeWithTvDBCrossRef.Contains(xref.AnimeID)) animeWithTvDBCrossRef.Add(xref.AnimeID);
                }
                ts = DateTime.Now - start;
                logger.Info("Get All AniDB->TvDB Cross Refs (Database) in {0} ms", ts.TotalMilliseconds);

                // MovieDB
                start = DateTime.Now;
                var repOtherCrossRef = new CrossRef_AniDB_OtherRepository();
                var allOtherCrossRefs = repOtherCrossRef.GetAll();
                var animeWithMovieCrossRef = new List<int>();
                foreach (var xref in allOtherCrossRefs)
                {
                    if (!animeWithMovieCrossRef.Contains(xref.AnimeID) &&
                        xref.CrossRefType == (int)CrossRefType.MovieDB)
                        animeWithMovieCrossRef.Add(xref.AnimeID);
                }
                ts = DateTime.Now - start;
                logger.Info("Get All AniDB->MovieDB Cross Refs (Database) in {0} ms", ts.TotalMilliseconds);


                // MAL
                start = DateTime.Now;
                var repMALCrossRef = new CrossRef_AniDB_MALRepository();
                var allMALCrossRefs = repMALCrossRef.GetAll();
                var animeWithMALCrossRef = new List<int>();
                foreach (var xref in allMALCrossRefs)
                {
                    if (!animeWithMALCrossRef.Contains(xref.AnimeID))
                        animeWithMALCrossRef.Add(xref.AnimeID);
                }
                ts = DateTime.Now - start;
                logger.Info("Get All AniDB->MAL Cross Refs (Database) in {0} ms", ts.TotalMilliseconds);

                #endregion

                start = DateTime.Now;
                var session = JMMService.SessionFactory.OpenSession();
                foreach (var ag in allGrps)
                {
                    // get all the series for this group
                    var seriesForGroup = new List<AnimeSeries>();
                    GetAnimeSeriesRecursive(ag, ref seriesForGroup, allSeries, allGroupsDict);
                    /*
					if (ag.AnimeGroupID == 915)
					{
						Console.Write("");
					}

                    */
                    DateTime? Stat_AirDate_Min = null;
                    DateTime? Stat_AirDate_Max = null;
                    DateTime? Stat_EndDate = new DateTime(1980, 1, 1);
                    DateTime? Stat_SeriesCreatedDate = null;
                    var isComplete = false;
                    var hasFinishedAiring = false;
                    var isCurrentlyAiring = false;

                    var tagIDList = new List<int>();
                    var customTagIDList = new List<int>();
                    var audioLanguageList = new List<string>();
                    var subtitleLanguageList = new List<string>();
                    var Stat_AllTitles = "";
                    var Stat_AllTags = "";
                    var Stat_AllCustomTags = "";
                    var Stat_AllVideoQualityEpisodes = "";


                    decimal totalVotesPerm = 0, totalVotesTemp = 0, totalVotes = 0;
                    int countVotesPerm = 0, countVotesTemp = 0, countVotes = 0;

                    var hasTvDB = true;
                    var hasMAL = true;
                    var hasMovieDB = true;
                    var hasMovieDBOrTvDB = true;

                    var seriesCount = 0;
                    var epCount = 0;

                    foreach (var series in seriesForGroup)
                    {
                        seriesCount++;
                        if (allAnimeDict.ContainsKey(series.AniDB_ID))
                        {
                            var thisAnime = allAnimeDict[series.AniDB_ID];

                            epCount = epCount + thisAnime.EpisodeCountNormal;

                            // All Video Quality Episodes
                            // Try to determine if this anime has all the episodes available at a certain video quality
                            // e.g.  the series has all episodes in blu-ray
                            if (dictStats.ContainsKey(series.AniDB_ID))
                            {
                                if (series.AniDB_ID == 7656)
                                {
                                    Debug.Print("");
                                }

                                var stat = dictStats[series.AniDB_ID];
                                foreach (var kvp in stat.VideoQualityEpisodeCount)
                                {
                                    if (kvp.Value >= thisAnime.EpisodeCountNormal)
                                    {
                                        if (Stat_AllVideoQualityEpisodes.Length > 0)
                                            Stat_AllVideoQualityEpisodes += ",";
                                        Stat_AllVideoQualityEpisodes += kvp.Key;
                                    }
                                }
                            }

                            // Calculate Air Date 
                            var thisDate = thisAnime.AirDate;
                            if (thisDate.HasValue)
                            {
                                if (Stat_AirDate_Min.HasValue)
                                {
                                    if (thisDate.Value < Stat_AirDate_Min.Value) Stat_AirDate_Min = thisDate;
                                }
                                else
                                    Stat_AirDate_Min = thisDate;

                                if (Stat_AirDate_Max.HasValue)
                                {
                                    if (thisDate.Value > Stat_AirDate_Max.Value) Stat_AirDate_Max = thisDate;
                                }
                                else
                                    Stat_AirDate_Max = thisDate;
                            }

                            // calculate end date
                            // if the end date is NULL it actually means it is ongoing, so this is the max possible value
                            thisDate = thisAnime.EndDate;
                            if (thisDate.HasValue && Stat_EndDate.HasValue)
                            {
                                if (thisDate.Value > Stat_EndDate.Value) Stat_EndDate = thisDate;
                            }
                            else
                                Stat_EndDate = null;

                            // Calculate Series Created Date 
                            thisDate = series.DateTimeCreated;
                            if (thisDate.HasValue)
                            {
                                if (Stat_SeriesCreatedDate.HasValue)
                                {
                                    if (thisDate.Value < Stat_SeriesCreatedDate.Value)
                                        Stat_SeriesCreatedDate = thisDate;
                                }
                                else
                                    Stat_SeriesCreatedDate = thisDate;
                            }
                            /*
							if (series.AniDB_ID == 2369)
								Debug.Write("Test");
                            */
                            // Note - only one series has to be finished airing to qualify
                            if (thisAnime.EndDate.HasValue && thisAnime.EndDate.Value < DateTime.Now)
                                hasFinishedAiring = true;

                            // Note - only one series has to be currently airing to qualify
                            if (!thisAnime.EndDate.HasValue || thisAnime.EndDate.Value > DateTime.Now)
                                isCurrentlyAiring = true;

                            // We evaluate IsComplete as true if
                            // 1. series has finished airing
                            // 2. user has all episodes locally
                            // Note - only one series has to be complete for the group to be considered complete
                            if (thisAnime.EndDate.HasValue)
                            {
                                if (thisAnime.EndDate.Value < DateTime.Now && series.MissingEpisodeCount == 0 &&
                                    series.MissingEpisodeCountGroups == 0)
                                {
                                    isComplete = true;
                                }
                            }

                            // get categories
                            if (allAnimeTagsDict.ContainsKey(series.AniDB_ID))
                            {
                                foreach (var catID in allAnimeTagsDict[series.AniDB_ID])
                                {
                                    if (!tagIDList.Contains(catID)) tagIDList.Add(catID);
                                }
                            }

                            // get custom tags
                            if (allCustomTagsXRefDict.ContainsKey(series.AniDB_ID))
                            {
                                foreach (var xtag in allCustomTagsXRefDict[series.AniDB_ID])
                                {
                                    if (!customTagIDList.Contains(xtag.CustomTagID))
                                        customTagIDList.Add(xtag.CustomTagID);
                                }
                            }

                            // get audio languages
                            if (dictAudioStats.ContainsKey(series.AniDB_ID))
                            {
                                foreach (var lanName in dictAudioStats[series.AniDB_ID].LanguageNames)
                                {
                                    if (!audioLanguageList.Contains(lanName)) audioLanguageList.Add(lanName);
                                }
                            }

                            // get subtitle languages
                            if (dictSubtitleStats.ContainsKey(series.AniDB_ID))
                            {
                                foreach (var lanName in dictSubtitleStats[series.AniDB_ID].LanguageNames)
                                {
                                    if (!subtitleLanguageList.Contains(lanName)) subtitleLanguageList.Add(lanName);
                                }
                            }

                            // get titles
                            if (allTitlesDict.ContainsKey(series.AniDB_ID))
                            {
                                foreach (var title in allTitlesDict[series.AniDB_ID])
                                {
                                    if (Stat_AllTitles.Length > 0) Stat_AllTitles += ",";
                                    Stat_AllTitles += title.Title;
                                }
                            }

                            // get votes
                            foreach (var vote in allVotes)
                            {
                                if (vote.EntityID == series.AniDB_ID &&
                                    (vote.VoteType == (int)AniDBVoteType.Anime ||
                                     vote.VoteType == (int)AniDBVoteType.AnimeTemp))
                                {
                                    countVotes++;
                                    totalVotes += vote.VoteValue;

                                    if (vote.VoteType == (int)AniDBVoteType.Anime)
                                    {
                                        countVotesPerm++;
                                        totalVotesPerm += vote.VoteValue;
                                    }
                                    if (vote.VoteType == (int)AniDBVoteType.AnimeTemp)
                                    {
                                        countVotesTemp++;
                                        totalVotesTemp += vote.VoteValue;
                                    }

                                    break;
                                }
                            }
                        }

                        // for the group, if any of the series don't have a tvdb link
                        // we will consider the group as not having a tvdb link
                        if (!animeWithTvDBCrossRef.Contains(series.AniDB_ID)) hasTvDB = false;
                        if (!animeWithMovieCrossRef.Contains(series.AniDB_ID)) hasMovieDB = false;
                        if (!animeWithMALCrossRef.Contains(series.AniDB_ID)) hasMAL = false;

                        if (!animeWithTvDBCrossRef.Contains(series.AniDB_ID) &&
                            !animeWithMovieCrossRef.Contains(series.AniDB_ID)) hasMovieDBOrTvDB = false;
                    }

                    if (allVidQuality.ContainsKey(ag.AnimeGroupID))
                        StatGroupVideoQuality[ag.AnimeGroupID] = allVidQuality[ag.AnimeGroupID];
                    else
                        StatGroupVideoQuality[ag.AnimeGroupID] = "";

                    StatGroupVideoQualityEpisodes[ag.AnimeGroupID] = Stat_AllVideoQualityEpisodes;

                    StatGroupIsComplete[ag.AnimeGroupID] = isComplete;
                    StatGroupIsFinishedAiring[ag.AnimeGroupID] = hasFinishedAiring;
                    StatGroupIsCurrentlyAiring[ag.AnimeGroupID] = isCurrentlyAiring;
                    StatGroupSeriesCount[ag.AnimeGroupID] = seriesCount;
                    StatGroupEpisodeCount[ag.AnimeGroupID] = epCount;

                    StatGroupTitles[ag.AnimeGroupID] = Stat_AllTitles;
                    StatGroupAirDate_Max[ag.AnimeGroupID] = Stat_AirDate_Max;
                    StatGroupAirDate_Min[ag.AnimeGroupID] = Stat_AirDate_Min;
                    StatGroupEndDate[ag.AnimeGroupID] = Stat_EndDate;
                    StatGroupSeriesCreatedDate[ag.AnimeGroupID] = Stat_SeriesCreatedDate;
                    StatGroupHasTvDB[ag.AnimeGroupID] = hasTvDB;
                    StatGroupHasMAL[ag.AnimeGroupID] = hasMAL;
                    StatGroupHasMovieDB[ag.AnimeGroupID] = hasMovieDB;
                    StatGroupHasMovieDBOrTvDB[ag.AnimeGroupID] = hasMovieDBOrTvDB;

                    decimal? Stat_UserVoteOverall = null;
                    if (countVotes > 0)
                        Stat_UserVoteOverall = totalVotes / countVotes / 100;
                    StatGroupUserVoteOverall[ag.AnimeGroupID] = Stat_UserVoteOverall;

                    decimal? Stat_UserVotePermanent = null;
                    if (countVotesPerm > 0)
                        Stat_UserVotePermanent = totalVotesPerm / countVotesPerm / 100;
                    StatGroupUserVotePermanent[ag.AnimeGroupID] = Stat_UserVotePermanent;

                    decimal? Stat_UserVoteTemporary = null;
                    if (countVotesTemp > 0)
                        Stat_UserVoteTemporary = totalVotesTemp / countVotesTemp / 100;
                    StatGroupUserVoteTemporary[ag.AnimeGroupID] = Stat_UserVoteTemporary;

                    StatGroupAniDBRating[ag.AnimeGroupID] = ag.AniDBRating;

                    // tags
                    Stat_AllTags = "";

                    foreach (var tagID in tagIDList)
                    {
                        if (!allTagsDict.ContainsKey(tagID)) continue;

                        var catName = allTagsDict[tagID].TagName;
                        if (Stat_AllTags.Length > 0)
                            Stat_AllTags += "|";

                        Stat_AllTags += catName;
                    }
                    StatGroupTags[ag.AnimeGroupID] = Stat_AllTags;

                    // custom tags
                    Stat_AllCustomTags = "";

                    foreach (var tagID in customTagIDList)
                    {
                        if (!allCustomTagsDict.ContainsKey(tagID)) continue;

                        var tagName = allCustomTagsDict[tagID].TagName;
                        if (Stat_AllCustomTags.Length > 0)
                            Stat_AllCustomTags += "|";

                        Stat_AllCustomTags += tagName;
                    }
                    StatGroupCustomTags[ag.AnimeGroupID] = Stat_AllCustomTags;

                    var Stat_AudioLanguages = "";
                    foreach (var audioLan in audioLanguageList)
                    {
                        if (Stat_AudioLanguages.Length > 0) Stat_AudioLanguages += ",";
                        Stat_AudioLanguages += audioLan;
                    }
                    StatGroupAudioLanguages[ag.AnimeGroupID] = Stat_AudioLanguages;

                    var Stat_SubtitleLanguages = "";
                    foreach (var subLan in subtitleLanguageList)
                    {
                        if (Stat_SubtitleLanguages.Length > 0) Stat_SubtitleLanguages += ",";
                        Stat_SubtitleLanguages += subLan;
                    }
                    StatGroupSubtitleLanguages[ag.AnimeGroupID] = Stat_SubtitleLanguages;

                    UpdateGroupFilterUsingGroup(ag.AnimeGroupID);
                    UpdatePlexAnimeGroup(session, ag, allSeries);
                    UpdateKodiAnimeGroup(session, ag, allSeries);
                }


                ts = DateTime.Now - start;
                logger.Info("GetAllGroups (Contracts) in {0} ms", ts.TotalMilliseconds);

                //UpdateAllAnimeContracts();
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
        }

        private static void GetAnimeSeriesRecursive(AnimeGroup grp, ref List<AnimeSeries> seriesList,
            List<AnimeSeries> allSeries, Dictionary<int, AnimeGroup> allGroupsDict)
        {
            // get the series for this group
            var thisSeries = new List<AnimeSeries>();
            foreach (var ser in allSeries)
                if (ser.AnimeGroupID == grp.AnimeGroupID) seriesList.Add(ser);


            foreach (var kvp in allGroupsDict)
            {
                if (kvp.Value.AnimeGroupParentID.HasValue && kvp.Value.AnimeGroupParentID.Value == grp.AnimeGroupID)
                {
                    GetAnimeSeriesRecursive(kvp.Value, ref seriesList, allSeries, allGroupsDict);
                }
            }

            /*foreach (AnimeGroup childGroup in grp.ChildGroups)
			{
				GetAnimeSeriesRecursive(childGroup, ref seriesList, allSeries, allGroupsDict);
			}*/
        }

        public bool EvaluateGroupFilter(GroupFilter gf, AnimeGroup grp, JMMUser curUser, AnimeGroup_User userRec)
        {
            // sub groups don't count
            if (grp.AnimeGroupParentID.HasValue) return false;

            // make sure the user has not filtered this out
            if (!curUser.AllowedGroup(grp, userRec)) return false;

            // first check for anime groups which are included exluded every time
            foreach (var gfc in gf.FilterConditions)
            {
                if (gfc.ConditionTypeEnum != GroupFilterConditionType.AnimeGroup) continue;

                var groupID = 0;
                int.TryParse(gfc.ConditionParameter, out groupID);
                if (groupID == 0) break;

                if (gfc.ConditionOperatorEnum == GroupFilterOperator.Equals)
                    if (groupID == grp.AnimeGroupID) return true;

                if (gfc.ConditionOperatorEnum == GroupFilterOperator.NotEquals)
                    if (groupID == grp.AnimeGroupID) return false;
            }

            var style = NumberStyles.Number;
            var culture = CultureInfo.CreateSpecificCulture("en-GB");

            if (gf.BaseCondition == (int)GroupFilterBaseCondition.Exclude) return false;

            var contractGroup = grp.ToContract(userRec);

            // now check other conditions
            foreach (var gfc in gf.FilterConditions)
            {
                switch (gfc.ConditionTypeEnum)
                {
                    case GroupFilterConditionType.Favourite:
                        if (userRec == null) return false;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include && userRec.IsFave == 0)
                            return false;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude && userRec.IsFave == 1)
                            return false;
                        break;

                    case GroupFilterConditionType.MissingEpisodes:
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include &&
                            grp.HasMissingEpisodesAny == false) return false;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude && grp.HasMissingEpisodesAny)
                            return false;
                        break;

                    case GroupFilterConditionType.MissingEpisodesCollecting:
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include &&
                            grp.HasMissingEpisodesGroups == false) return false;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude && grp.HasMissingEpisodesGroups)
                            return false;
                        break;

                    case GroupFilterConditionType.HasWatchedEpisodes:
                        if (userRec == null) return false;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include && userRec.AnyFilesWatched == false)
                            return false;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude && userRec.AnyFilesWatched)
                            return false;
                        break;

                    case GroupFilterConditionType.HasUnwatchedEpisodes:
                        if (userRec == null) return false;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include &&
                            userRec.HasUnwatchedFiles == false) return false;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude && userRec.HasUnwatchedFiles)
                            return false;
                        break;

                    case GroupFilterConditionType.AssignedTvDBInfo:
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include &&
                            contractGroup.Stat_HasTvDBLink == false) return false;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude && contractGroup.Stat_HasTvDBLink)
                            return false;
                        break;

                    case GroupFilterConditionType.AssignedMALInfo:
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include &&
                            contractGroup.Stat_HasMALLink == false) return false;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude && contractGroup.Stat_HasMALLink)
                            return false;
                        break;

                    case GroupFilterConditionType.AssignedMovieDBInfo:
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include &&
                            contractGroup.Stat_HasMovieDBLink == false) return false;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude &&
                            contractGroup.Stat_HasMovieDBLink) return false;
                        break;

                    case GroupFilterConditionType.AssignedTvDBOrMovieDBInfo:
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include &&
                            contractGroup.Stat_HasMovieDBOrTvDBLink == false) return false;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude &&
                            contractGroup.Stat_HasMovieDBOrTvDBLink) return false;
                        break;

                    case GroupFilterConditionType.CompletedSeries:

                        /*if (grp.IsComplete != grp.Stat_IsComplete)
						{
							Debug.Print("IsComplete DIFF  {0}", grp.GroupName);
						}*/

                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include &&
                            contractGroup.Stat_IsComplete == false) return false;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude && contractGroup.Stat_IsComplete)
                            return false;
                        break;

                    case GroupFilterConditionType.FinishedAiring:
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include &&
                            contractGroup.Stat_HasFinishedAiring == false) return false;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude &&
                            contractGroup.Stat_IsCurrentlyAiring == false) return false;
                        break;

                    case GroupFilterConditionType.UserVoted:
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include &&
                            contractGroup.Stat_UserVotePermanent.HasValue == false) return false;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude &&
                            contractGroup.Stat_UserVotePermanent.HasValue) return false;
                        break;

                    case GroupFilterConditionType.UserVotedAny:
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Include &&
                            contractGroup.Stat_UserVoteOverall.HasValue == false) return false;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.Exclude &&
                            contractGroup.Stat_UserVoteOverall.HasValue) return false;
                        break;

                    case GroupFilterConditionType.AirDate:
                        DateTime filterDate;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.LastXDays)
                        {
                            var days = 0;
                            int.TryParse(gfc.ConditionParameter, out days);
                            filterDate = DateTime.Today.AddDays(0 - days);
                        }
                        else
                            filterDate = GetDateFromString(gfc.ConditionParameter);

                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.GreaterThan ||
                            gfc.ConditionOperatorEnum == GroupFilterOperator.LastXDays)
                        {
                            if (!contractGroup.Stat_AirDate_Min.HasValue || !contractGroup.Stat_AirDate_Max.HasValue)
                                return false;
                            if (contractGroup.Stat_AirDate_Max.Value < filterDate) return false;
                        }
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.LessThan)
                        {
                            if (!contractGroup.Stat_AirDate_Min.HasValue || !contractGroup.Stat_AirDate_Max.HasValue)
                                return false;
                            if (contractGroup.Stat_AirDate_Min.Value > filterDate) return false;
                        }
                        break;

                    case GroupFilterConditionType.LatestEpisodeAirDate:
                        DateTime filterDateEpisodeLatest;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.LastXDays)
                        {
                            var days = 0;
                            int.TryParse(gfc.ConditionParameter, out days);
                            filterDateEpisodeLatest = DateTime.Today.AddDays(0 - days);
                        }
                        else
                            filterDateEpisodeLatest = GetDateFromString(gfc.ConditionParameter);

                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.GreaterThan ||
                            gfc.ConditionOperatorEnum == GroupFilterOperator.LastXDays)
                        {
                            if (!contractGroup.LatestEpisodeAirDate.HasValue) return false;
                            if (contractGroup.LatestEpisodeAirDate.Value < filterDateEpisodeLatest) return false;
                        }
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.LessThan)
                        {
                            if (!contractGroup.LatestEpisodeAirDate.HasValue) return false;
                            if (contractGroup.LatestEpisodeAirDate.Value > filterDateEpisodeLatest) return false;
                        }
                        break;

                    case GroupFilterConditionType.SeriesCreatedDate:
                        DateTime filterDateSeries;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.LastXDays)
                        {
                            var days = 0;
                            int.TryParse(gfc.ConditionParameter, out days);
                            filterDateSeries = DateTime.Today.AddDays(0 - days);
                        }
                        else
                            filterDateSeries = GetDateFromString(gfc.ConditionParameter);

                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.GreaterThan ||
                            gfc.ConditionOperatorEnum == GroupFilterOperator.LastXDays)
                        {
                            if (!contractGroup.Stat_SeriesCreatedDate.HasValue) return false;
                            if (contractGroup.Stat_SeriesCreatedDate.Value < filterDateSeries) return false;
                        }
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.LessThan)
                        {
                            if (!contractGroup.Stat_SeriesCreatedDate.HasValue) return false;
                            if (contractGroup.Stat_SeriesCreatedDate.Value > filterDateSeries) return false;
                        }
                        break;

                    case GroupFilterConditionType.EpisodeWatchedDate:
                        DateTime filterDateEpsiodeWatched;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.LastXDays)
                        {
                            var days = 0;
                            int.TryParse(gfc.ConditionParameter, out days);
                            filterDateEpsiodeWatched = DateTime.Today.AddDays(0 - days);
                        }
                        else
                            filterDateEpsiodeWatched = GetDateFromString(gfc.ConditionParameter);

                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.GreaterThan ||
                            gfc.ConditionOperatorEnum == GroupFilterOperator.LastXDays)
                        {
                            if (userRec == null) return false;
                            if (!userRec.WatchedDate.HasValue) return false;
                            if (userRec.WatchedDate.Value < filterDateEpsiodeWatched) return false;
                        }
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.LessThan)
                        {
                            if (userRec == null) return false;
                            if (!userRec.WatchedDate.HasValue) return false;
                            if (userRec.WatchedDate.Value > filterDateEpsiodeWatched) return false;
                        }
                        break;

                    case GroupFilterConditionType.EpisodeAddedDate:
                        DateTime filterDateEpisodeAdded;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.LastXDays)
                        {
                            var days = 0;
                            int.TryParse(gfc.ConditionParameter, out days);
                            filterDateEpisodeAdded = DateTime.Today.AddDays(0 - days);
                        }
                        else
                            filterDateEpisodeAdded = GetDateFromString(gfc.ConditionParameter);

                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.GreaterThan ||
                            gfc.ConditionOperatorEnum == GroupFilterOperator.LastXDays)
                        {
                            if (!grp.EpisodeAddedDate.HasValue) return false;
                            if (grp.EpisodeAddedDate.Value < filterDateEpisodeAdded) return false;
                        }
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.LessThan)
                        {
                            if (!grp.EpisodeAddedDate.HasValue) return false;
                            if (grp.EpisodeAddedDate.Value > filterDateEpisodeAdded) return false;
                        }
                        break;

                    case GroupFilterConditionType.EpisodeCount:

                        var epCount = -1;
                        int.TryParse(gfc.ConditionParameter, out epCount);

                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.GreaterThan &&
                            contractGroup.Stat_EpisodeCount < epCount) return false;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.LessThan &&
                            contractGroup.Stat_EpisodeCount > epCount) return false;
                        break;

                    case GroupFilterConditionType.AniDBRating:

                        decimal dRating = -1;
                        decimal.TryParse(gfc.ConditionParameter, style, culture, out dRating);

                        var thisRating = contractGroup.Stat_AniDBRating / 100;

                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.GreaterThan && thisRating < dRating)
                            return false;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.LessThan && thisRating > dRating)
                            return false;
                        break;

                    case GroupFilterConditionType.UserRating:

                        if (!contractGroup.Stat_UserVoteOverall.HasValue) return false;

                        decimal dUserRating = -1;
                        decimal.TryParse(gfc.ConditionParameter, style, culture, out dUserRating);

                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.GreaterThan &&
                            contractGroup.Stat_UserVoteOverall.Value < dUserRating) return false;
                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.LessThan &&
                            contractGroup.Stat_UserVoteOverall.Value > dUserRating) return false;
                        break;

                    case GroupFilterConditionType.Category:

                        var filterParm = gfc.ConditionParameter.Trim();

                        var cats = filterParm.Split(',');
                        var foundCat = false;
                        var index = 0;
                        foreach (var cat in cats)
                        {
                            if (cat.Trim().Length == 0) continue;
                            if (cat.Trim() == ",") continue;

                            index = contractGroup.Stat_AllTags.IndexOf(cat.Trim(), 0,
                                StringComparison.InvariantCultureIgnoreCase);
                            if (index > -1)
                            {
                                foundCat = true;
                                break;
                            }
                        }

                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.In)
                            if (!foundCat) return false;

                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.NotIn)
                            if (foundCat) return false;
                        break;

                    case GroupFilterConditionType.CustomTags:

                        filterParm = gfc.ConditionParameter.Trim();

                        var tags = filterParm.Split(',');
                        var foundTag = false;
                        index = 0;
                        foreach (var tag in tags)
                        {
                            if (tag.Trim().Length == 0) continue;
                            if (tag.Trim() == ",") continue;

                            index = contractGroup.Stat_AllCustomTags.IndexOf(tag.Trim(), 0,
                                StringComparison.InvariantCultureIgnoreCase);
                            if (index > -1)
                            {
                                foundTag = true;
                                break;
                            }
                        }

                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.In)
                            if (!foundTag) return false;

                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.NotIn)
                            if (foundTag) return false;
                        break;

                    case GroupFilterConditionType.AnimeType:

                        filterParm = gfc.ConditionParameter.Trim();
                        var grpTypeList = grp.AnimeTypesList;

                        var atypes = filterParm.Split(',');
                        var foundAnimeType = false;
                        index = 0;
                        foreach (var atype in atypes)
                        {
                            if (atype.Trim().Length == 0) continue;
                            if (atype.Trim() == ",") continue;

                            foreach (var thisAType in grpTypeList)
                            {
                                if (string.Equals(thisAType, atype, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    foundAnimeType = true;
                                    break;
                                }
                            }
                        }

                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.In)
                            if (!foundAnimeType) return false;

                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.NotIn)
                            if (foundAnimeType) return false;
                        break;


                    case GroupFilterConditionType.VideoQuality:

                        filterParm = gfc.ConditionParameter.Trim();

                        var vidQuals = filterParm.Split(',');
                        var foundVid = false;
                        var foundVidAllEps = false;
                        index = 0;
                        foreach (var vidq in vidQuals)
                        {
                            if (vidq.Trim().Length == 0) continue;
                            if (vidq.Trim() == ",") continue;

                            index = contractGroup.Stat_AllVideoQuality.IndexOf(vidq, 0,
                                StringComparison.InvariantCultureIgnoreCase);
                            if (index > -1) foundVid = true;

                            index = contractGroup.Stat_AllVideoQuality_Episodes.IndexOf(vidq, 0,
                                StringComparison.InvariantCultureIgnoreCase);
                            if (index > -1) foundVidAllEps = true;
                        }

                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.In)
                            if (!foundVid) return false;

                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.NotIn)
                            if (foundVid) return false;

                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.InAllEpisodes)
                            if (!foundVidAllEps) return false;

                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.NotInAllEpisodes)
                            if (foundVidAllEps) return false;

                        break;

                    case GroupFilterConditionType.AudioLanguage:
                    case GroupFilterConditionType.SubtitleLanguage:

                        filterParm = gfc.ConditionParameter.Trim();

                        var languages = filterParm.Split(',');
                        var foundLan = false;
                        index = 0;
                        foreach (var lanName in languages)
                        {
                            if (lanName.Trim().Length == 0) continue;
                            if (lanName.Trim() == ",") continue;

                            if (gfc.ConditionTypeEnum == GroupFilterConditionType.AudioLanguage)
                                index = contractGroup.Stat_AudioLanguages.IndexOf(lanName, 0,
                                    StringComparison.InvariantCultureIgnoreCase);

                            if (gfc.ConditionTypeEnum == GroupFilterConditionType.SubtitleLanguage)
                                index = contractGroup.Stat_SubtitleLanguages.IndexOf(lanName, 0,
                                    StringComparison.InvariantCultureIgnoreCase);

                            if (index > -1) foundLan = true;
                        }

                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.In)
                            if (!foundLan) return false;

                        if (gfc.ConditionOperatorEnum == GroupFilterOperator.NotIn)
                            if (foundLan) return false;

                        break;
                }
            }

            return true;
        }

        public static DateTime GetDateFromString(string sDate)
        {
            try
            {
                var year = int.Parse(sDate.Substring(0, 4));
                var month = int.Parse(sDate.Substring(4, 2));
                var day = int.Parse(sDate.Substring(6, 2));

                return new DateTime(year, month, day);
            }
            catch (Exception ex)
            {
                return DateTime.Today;
            }
        }

        /*private static void GetAnimeSeriesRecursive(int animeGroupID, ref List<AnimeSeries> seriesList, List<AnimeSeries> allSeries)
		{
			AnimeGroupRepository rep = new AnimeGroupRepository();
			AnimeGroup grp = rep.GetByID(animeGroupID);
			if (grp == null) return;

			// get the series for this group
			List<AnimeSeries> thisSeries = new List<AnimeSeries>();
			foreach (AnimeSeries ser in allSeries)
				if (ser.AnimeGroupID == animeGroupID) seriesList.Add(ser);


			foreach (AnimeGroup childGroup in grp.ChildGroups)
			{
				GetAnimeSeriesRecursive(childGroup.AnimeGroupID, ref seriesList, allSeries);
			}
		}*/
    }
}
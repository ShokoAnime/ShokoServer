using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMContracts;
using JMMServer.Databases;
using JMMServer.Entities;
using JMMServer.Kodi;
using JMMServer.Plex;
using Newtonsoft.Json;
using NHibernate.Criterion;
using NLog;
using NHibernate;
using NHibernate.Linq;
using NutzCode.InMemoryIndex;

namespace JMMServer.Repositories
{
	public class AnimeGroupRepository
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

	    private static PocoCache<int, AnimeGroup> Cache;
	    private static PocoIndex<int, AnimeGroup, int> Parents;
         
	    public static void InitCache()
	    {
            string t = "AnimeGroups";
            ServerState.Instance.CurrentSetupStatus = string.Format(DatabaseHelper.InitCacheTitle, t, string.Empty);
            AnimeGroupRepository repo =new AnimeGroupRepository();
	        Cache = new PocoCache<int, AnimeGroup>(repo.InternalGetAll(), a => a.AnimeGroupID);
            Parents = Cache.CreateIndex(a=>a.AnimeGroupParentID ?? 0);
	        List<AnimeGroup> grps = Cache.Values.Where(a => a.ContractVersion < AnimeGroup.CONTRACT_VERSION).ToList();
            int max = grps.Count;
            int cnt = 0;
            foreach (AnimeGroup g in grps)
            {
                repo.Save(g, true,false);
                cnt++;
                if (cnt % 10 == 0)
                {
                    ServerState.Instance.CurrentSetupStatus = string.Format(DatabaseHelper.InitCacheTitle, t, " DbRegen - " + cnt + "/" + max);
                }
            }	     
            ServerState.Instance.CurrentSetupStatus = string.Format(DatabaseHelper.InitCacheTitle, t, " DbRegen - " + max + "/" + max);
        }

        public List<AnimeGroup> InternalGetAll()
	    {
	        using (var session = JMMService.SessionFactory.OpenSession())
	        {
	            var grps = session
	                .CreateCriteria(typeof (AnimeGroup))
	                .List<AnimeGroup>();

	            return new List<AnimeGroup>(grps);
	        }
	    }


        public void UpdateContract(ISession session, AnimeGroup grp, bool updatestats)
	    {
            Contract_AnimeGroup contract = grp.Contract;
            if (contract == null)
            {
                contract = new Contract_AnimeGroup();
                updatestats = true;
            }
            contract.AnimeGroupID = grp.AnimeGroupID;
            contract.AnimeGroupParentID = grp.AnimeGroupParentID;
            contract.DefaultAnimeSeriesID = grp.DefaultAnimeSeriesID;
            contract.GroupName = grp.GroupName;
            contract.Description = grp.Description;
            contract.SortName = grp.SortName;
            contract.EpisodeAddedDate = grp.EpisodeAddedDate;
            contract.OverrideDescription = grp.OverrideDescription;
            contract.DateTimeUpdated = grp.DateTimeUpdated;
            contract.IsFave = 0;
            contract.UnwatchedEpisodeCount = 0;
            contract.WatchedEpisodeCount = 0;
            contract.WatchedDate = null;
            contract.PlayedCount = 0;
            contract.WatchedCount = 0;
            contract.StoppedCount = 0;
            contract.MissingEpisodeCount = grp.MissingEpisodeCount;
            contract.MissingEpisodeCountGroups = grp.MissingEpisodeCountGroups;

            List<AnimeSeries> series = grp.GetAllSeries(session);
            if (updatestats)
            {
                DateTime? airDate_Min = null;
                DateTime? airDate_Max = null;
                DateTime? endDate = new DateTime(1980, 1, 1);
                DateTime? seriesCreatedDate = null;
                bool isComplete = false;
                bool hasFinishedAiring = false;
                bool isCurrentlyAiring = false;
                string videoQualityEpisodes = "";
                List<string> audioLanguages = new List<string>();
                List<string> subtitleLanguages = new List<string>();
                bool hasTvDB = true;
                bool hasMAL = true;
                bool hasMovieDB = true;
                bool hasMovieDBOrTvDB = true;
                int seriesCount = 0;
                int epCount = 0;
                AdhocRepository repAdHoc = new AdhocRepository();
                VideoLocalRepository repVids = new VideoLocalRepository();
                CrossRef_File_EpisodeRepository repXrefs = new CrossRef_File_EpisodeRepository();
                foreach (AnimeSeries serie in series)
                {
                    seriesCount++;
                    List<VideoLocal> vidsTemp = repVids.GetByAniDBAnimeID(session, serie.AniDB_ID);
                    List<CrossRef_File_Episode> crossRefs = repXrefs.GetByAnimeID(session, serie.AniDB_ID);

                    Dictionary<int, List<CrossRef_File_Episode>> dictCrossRefs =
                        new Dictionary<int, List<CrossRef_File_Episode>>();
                    foreach (CrossRef_File_Episode xref in crossRefs)
                    {
                        if (!dictCrossRefs.ContainsKey(xref.EpisodeID))
                            dictCrossRefs[xref.EpisodeID] = new List<CrossRef_File_Episode>();
                        dictCrossRefs[xref.EpisodeID].Add(xref);
                    }

                    Dictionary<string, VideoLocal> dictVids = new Dictionary<string, VideoLocal>();
                    foreach (VideoLocal vid in vidsTemp)
                        dictVids[vid.Hash] = vid;

                    // All Video Quality Episodes
                    // Try to determine if this anime has all the episodes available at a certain video quality
                    // e.g.  the series has all episodes in blu-ray
                    // Also look at languages
                    Dictionary<string, int> vidQualEpCounts = new Dictionary<string, int>();
                    // video quality, count of episodes

                    foreach (AnimeEpisode ep in serie.GetAnimeEpisodes(session))
                    {
                        if (ep.EpisodeTypeEnum != AniDBAPI.enEpisodeType.Episode) continue;


                        List<VideoLocal> epVids = new List<VideoLocal>();
                        if (dictCrossRefs.ContainsKey(ep.AniDB_EpisodeID))
                        {
                            foreach (CrossRef_File_Episode xref in dictCrossRefs[ep.AniDB_EpisodeID])
                            {
                                if (xref.EpisodeID == ep.AniDB_EpisodeID)
                                {
                                    if (dictVids.ContainsKey(xref.Hash))
                                        epVids.Add(dictVids[xref.Hash]);
                                }
                            }
                        }

                        List<string> qualityAddedSoFar = new List<string>();
                        // handle mutliple files of the same quality for one episode
                        foreach (VideoLocal vid in epVids)
                        {
                            AniDB_File anifile = vid.GetAniDBFile(session);
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
                    AniDB_Anime anime = serie.GetAnime(session);
                    epCount = epCount + anime.EpisodeCountNormal;

                    foreach (KeyValuePair<string, int> kvp in vidQualEpCounts)
                    {
                        int index = videoQualityEpisodes.IndexOf(kvp.Key, 0,
                            StringComparison.InvariantCultureIgnoreCase);
                        if (index > -1) continue; // don't add if we already have it

                        if (anime.EpisodeCountNormal == kvp.Value)
                        {
                            if (videoQualityEpisodes.Length > 0) videoQualityEpisodes += ",";
                            videoQualityEpisodes += kvp.Key;
                        }

                    }
                    // audio languages
                    Dictionary<int, LanguageStat> dicAudio = repAdHoc.GetAudioLanguageStatsByAnime(session,
                        anime.AnimeID);
                    foreach (KeyValuePair<int, LanguageStat> kvp in dicAudio)
                    {
                        foreach (string lanName in kvp.Value.LanguageNames)
                        {
                            if (!audioLanguages.Contains(lanName))
                                audioLanguages.Add(lanName);
                        }
                    }
                    // subtitle languages
                    Dictionary<int, LanguageStat> dicSubtitle = repAdHoc.GetSubtitleLanguageStatsByAnime(session,
                        anime.AnimeID);
                    foreach (KeyValuePair<int, LanguageStat> kvp in dicSubtitle)
                    {
                        foreach (string lanName in kvp.Value.LanguageNames)
                        {
                            if (!subtitleLanguages.Contains(lanName))
                                subtitleLanguages.Add(lanName);
                        }
                    }

                    // Calculate Air Date 
                    DateTime? thisDate = serie.AirDate;
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
                    thisDate = serie.EndDate;
                    if (thisDate.HasValue && endDate.HasValue)
                    {
                        if (thisDate.Value > endDate.Value) endDate = thisDate;
                    }
                    else
                        endDate = null;

                    // Note - only one series has to be finished airing to qualify
                    if (serie.EndDate.HasValue && serie.EndDate.Value < DateTime.Now)
                        hasFinishedAiring = true;

                    // Note - only one series has to be finished airing to qualify
                    if (!serie.EndDate.HasValue || serie.EndDate.Value > DateTime.Now)
                        isCurrentlyAiring = true;

                    // We evaluate IsComplete as true if
                    // 1. series has finished airing
                    // 2. user has all episodes locally
                    // Note - only one series has to be complete for the group to be considered complete
                    if (serie.EndDate.HasValue)
                    {
                        if (serie.EndDate.Value < DateTime.Now && serie.MissingEpisodeCount == 0 &&
                            serie.MissingEpisodeCountGroups == 0)
                        {
                            isComplete = true;
                        }
                    }

                    // Calculate Series Created Date 
                    thisDate = serie.DateTimeCreated;
                    if (thisDate.HasValue)
                    {
                        if (seriesCreatedDate.HasValue)
                        {
                            if (thisDate.Value < seriesCreatedDate.Value) seriesCreatedDate = thisDate;
                        }
                        else
                            seriesCreatedDate = thisDate;
                    }
                    // for the group, if any of the series don't have a tvdb link
                    // we will consider the group as not having a tvdb link

                    List<CrossRef_AniDB_TvDBV2> tvXrefs = serie.GetCrossRefTvDBV2();

                    if (tvXrefs == null || tvXrefs.Count == 0) hasTvDB = false;
                    if (serie.CrossRefMovieDB == null) hasMovieDB = false;
                    if (serie.CrossRefMAL == null) hasMAL = false;

                    if ((tvXrefs == null || tvXrefs.Count == 0) && serie.CrossRefMovieDB == null)
                        hasMovieDBOrTvDB = false;
                }


                string Stat_AudioLanguages = "";
                foreach (string audioLan in audioLanguages)
                {
                    if (Stat_AudioLanguages.Length > 0) Stat_AudioLanguages += ",";
                    Stat_AudioLanguages += audioLan;
                }

                string Stat_SubtitleLanguages = "";
                foreach (string subLan in subtitleLanguages)
                {
                    if (Stat_SubtitleLanguages.Length > 0) Stat_SubtitleLanguages += ",";
                    Stat_SubtitleLanguages += subLan;
                }
                contract.Stat_AllTags = grp.TagsString;
                contract.Stat_AllCustomTags = grp.CustomTagsString;
                contract.Stat_AllTitles = grp.TitlesString;
                contract.Stat_AllVideoQuality = grp.VideoQualityString;
                contract.Stat_IsComplete = isComplete;
                contract.Stat_HasFinishedAiring = hasFinishedAiring;
                contract.Stat_IsCurrentlyAiring = isCurrentlyAiring;
                contract.Stat_HasTvDBLink = hasTvDB;
                contract.Stat_HasMALLink = hasMAL;
                contract.Stat_HasMovieDBLink = hasMovieDB;
                contract.Stat_HasMovieDBOrTvDBLink = hasMovieDBOrTvDB;
                contract.Stat_SeriesCount = seriesCount;
                contract.Stat_EpisodeCount = epCount;
                contract.Stat_AllVideoQuality_Episodes = videoQualityEpisodes;
                contract.Stat_AirDate_Min = airDate_Min;
                contract.Stat_AirDate_Max = airDate_Max;
                contract.Stat_EndDate = endDate;
                contract.Stat_SeriesCreatedDate = seriesCreatedDate;
                contract.Stat_UserVoteOverall = grp.UserVote;
                contract.Stat_UserVotePermanent = grp.UserVotePermanent;
                contract.Stat_UserVoteTemporary = grp.UserVoteTemporary;
                contract.Stat_AniDBRating = grp.AniDBRating;
                contract.Stat_AudioLanguages = Stat_AudioLanguages;
                contract.Stat_SubtitleLanguages = Stat_SubtitleLanguages;
            }
            grp.Contract = contract;
        }


        public void Save(AnimeGroup grp, bool updategrpcontractstats, bool recursive)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                UpdateContract(session, grp, updategrpcontractstats);
                if (grp.AnimeGroupParentID.HasValue && recursive)
                {
                    //TODO Introduced possible BUG, if a circular GroupParent is created, this will run infinite
                    AnimeGroup pgroup = GetByID(session, grp.AnimeGroupParentID.Value);
                    Save(pgroup, updategrpcontractstats, true);
                }
                using (var transaction = session.BeginTransaction())
                {
                    session.SaveOrUpdate(grp);
                    transaction.Commit();
                }
                Cache.Update(grp);
            }
        }

        public AnimeGroup GetByID(int id)
        {
            return Cache.Get(id);
		}

		public AnimeGroup GetByID(ISession session, int id)
		{
            return GetByID(id);
		}

		public List<AnimeGroup> GetByParentID(int parentid)
		{
		    return Parents.GetMultiple(parentid);
		}

		public List<AnimeGroup> GetByParentID(ISession session, int parentid)
		{
		    return GetByParentID(parentid);
		}

		public List<AnimeGroup> GetAll()
		{
		    return Cache.Values.ToList();
		}

		public List<AnimeGroup> GetAll(ISession session)
		{
		    return GetAll();
        }

        public List<AnimeGroup> GetAllTopLevelGroups()
        {
            return Parents.GetMultiple(0);
        }
        public List<AnimeGroup> GetAllTopLevelGroups(ISession session)
        {
            return GetAllTopLevelGroups();
        }
		public void Delete(int id)
		{
			AnimeGroup cr = GetByID(id);
            if (cr != null)
			{
                Cache.Remove(cr);
                // delete user records
                AnimeGroup_UserRepository repUsers = new AnimeGroup_UserRepository();
				foreach (AnimeGroup_User grpUser in repUsers.GetByGroupID(id))
					repUsers.Delete(grpUser.AnimeGroup_UserID);
			}

			int parentID = 0;
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				// populate the database
				using (var transaction = session.BeginTransaction())
				{
					if (cr != null)
					{
						if (cr.AnimeGroupParentID.HasValue) parentID = cr.AnimeGroupParentID.Value;
						session.Delete(cr);
						transaction.Commit();
					}
				}
			}

			if (parentID > 0)
			{
				logger.Trace("Updating group stats by group from AnimeGroupRepository.Delete: {0}", parentID);
			    AnimeGroup ngrp = GetByID(parentID);
			    if (ngrp != null)
			        this.Save(ngrp, false, true);
			}
		}
	}
}

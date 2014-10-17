using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;
using JMMServer.Repositories;
using JMMServer.Entities;
using System.Xml;
using JMMServer.Commands;
using System.IO;
using System.Net;
using BinaryNorthwest;
using JMMContracts;
using NHibernate;
using AniDBAPI;

namespace JMMServer.Providers.TraktTV
{
	public class TraktTVHelper
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		public static TraktTVShow GetShowInfo(string traktID)
		{
			TraktTVShow tvshow = new TraktTVShow();

			try
			{
				string url = string.Format(Constants.TraktTvURLs.URLGetShowExtended, Constants.TraktTvURLs.APIKey, traktID);
				logger.Trace("GetShowInfo: {0}", url);

				// Search for a series
				string json = Utils.DownloadWebPage(url);

				if (json.Trim().Length == 0) return null;

				tvshow = JSONHelper.Deserialize<TraktTVShow>(json);

				// save this data to the DB for use later
				SaveExtendedShowInfo(tvshow);

			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in TraktTVHelper.GetShowInfo: " + ex.ToString(), ex);
				return null;
			}

			return tvshow;
		}

		public static List<TraktTVFriendRequest> GetFriendsRequests()
		{
			List<TraktTVFriendRequest> friends = new List<TraktTVFriendRequest>();

			try
			{
				string url = string.Format(Constants.TraktTvURLs.URLPostFriendsRequests, Constants.TraktTvURLs.APIKey);
				logger.Trace("GetFriendsRequests: {0}", url);

				TraktTVPost_FriendsRequests cmd = new TraktTVPost_FriendsRequests();
				cmd.Init();

				string json = JSONHelper.Serialize<TraktTVPost_FriendsRequests>(cmd);
				string jsonResponse = SendData(url, json);
				if (string.IsNullOrEmpty(jsonResponse)) return friends;

				friends = JSONHelper.Deserialize<List<TraktTVFriendRequest>>(jsonResponse);


			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in TraktTVHelper.GetFriends: " + ex.ToString(), ex);
				return null;
			}

			return friends;
		}

		public static bool PostShoutShow(string traktID, string shoutText, bool isSpoiler, ref string returnMessage)
		{
			returnMessage = "";
			try
			{
				if (string.IsNullOrEmpty(ServerSettings.Trakt_Username) || string.IsNullOrEmpty(ServerSettings.Trakt_Password))
				{
					returnMessage = "Trakt credentials have not been entered";
					return false;
				}

				if (string.IsNullOrEmpty(shoutText))
				{
					returnMessage = "Please enter text for your shout";
					return false;
				}

				Trakt_ShowRepository repTraktShow = new Trakt_ShowRepository();

                Trakt_Show show = repTraktShow.GetByTraktID(traktID);
				if (show == null || !show.TvDB_ID.HasValue)
				{
                    returnMessage = string.Format("Could not find trakt show for : {0}", traktID);
					return false;
				}

				TraktTVPost_ShoutShow cmd = new TraktTVPost_ShoutShow();
				cmd.Init(shoutText, isSpoiler, show.TvDB_ID.Value);

				string url = string.Format(Constants.TraktTvURLs.URLPostShoutShow, Constants.TraktTvURLs.APIKey);
				logger.Trace("PostShoutShow: {0}", url);

				string json = JSONHelper.Serialize<TraktTVPost_ShoutShow>(cmd);
				string jsonResponse = SendData(url, json);

				TraktTVGenericResponse genResponse = JSONHelper.Deserialize<TraktTVGenericResponse>(jsonResponse);
				if (genResponse.IsSuccess)
				{
					returnMessage = genResponse.message;
					return true;
				}
				else
				{
					returnMessage = genResponse.error;
					return false;
				}

			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in TraktTVHelper.PostShoutShow: " + ex.ToString(), ex);
				returnMessage = ex.Message;
				return false;
			}

            return true;
		}

		public static List<TraktTV_ShoutGet> GetShowShouts(int animeID)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return GetShowShouts(session, animeID);
			}
		}

		public static List<TraktTV_ShoutGet> GetShowShouts(ISession session, int animeID)
		{
			List<TraktTV_ShoutGet> ret = new List<TraktTV_ShoutGet>();
			try
			{
				if (string.IsNullOrEmpty(ServerSettings.Trakt_Username) || string.IsNullOrEmpty(ServerSettings.Trakt_Password))
					return null;

                CrossRef_AniDB_TraktV2Repository repXrefTrakt = new CrossRef_AniDB_TraktV2Repository();
				List<CrossRef_AniDB_TraktV2> traktXRefs = repXrefTrakt.GetByAnimeID(session, animeID);
				if (traktXRefs == null || traktXRefs.Count == 0) return null;

                // get a unique list of trakt id's
                List<string> ids = new List<string>();
                foreach (CrossRef_AniDB_TraktV2 xref in traktXRefs)
                {
                    if (!ids.Contains(xref.TraktID))
                        ids.Add(xref.TraktID);
                }

                foreach (string id in ids)
                {
                    string url = string.Format(Constants.TraktTvURLs.URLGetShowShouts, Constants.TraktTvURLs.APIKey, id);
                    logger.Trace("GetShowShouts: {0}", url);

                    // Search for a series
                    string json = Utils.DownloadWebPage(url);

                    if (json.Trim().Length == 0) return new List<TraktTV_ShoutGet>();

                    List<TraktTV_ShoutGet>  shouts = JSONHelper.Deserialize<List<TraktTV_ShoutGet>>(json);

                    Trakt_FriendRepository repFriends = new Trakt_FriendRepository();
                    foreach (TraktTV_ShoutGet shout in shouts)
                    {
                        ret.Add(shout);
                        Trakt_Friend traktFriend = repFriends.GetByUsername(session, shout.user.username);
                        if (traktFriend == null)
                        {
                            traktFriend = new Trakt_Friend();
                            traktFriend.LastAvatarUpdate = DateTime.Now;
                        }

                        traktFriend.Populate(shout.user);
                        repFriends.Save(traktFriend);

                        if (!string.IsNullOrEmpty(traktFriend.FullImagePath))
                        {
                            bool fileExists = File.Exists(traktFriend.FullImagePath);
                            TimeSpan ts = DateTime.Now - traktFriend.LastAvatarUpdate;

                            if (!fileExists || ts.TotalHours > 8)
                            {
                                traktFriend.LastAvatarUpdate = DateTime.Now;
                                CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(traktFriend.Trakt_FriendID, JMMImageType.Trakt_Friend, true);
                                cmd.Save(session);
                            }
                        }
                    }
                }

			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in TraktTVHelper.GetShowShouts: " + ex.ToString(), ex);
			}

			return ret;
		}

		public static TraktTV_ActivitySummary GetActivityFriends(bool shoutsOnly)
		{
			TraktTV_ActivitySummary summ = null;
			try
			{
				if (string.IsNullOrEmpty(ServerSettings.Trakt_Username) || string.IsNullOrEmpty(ServerSettings.Trakt_Password))
					return null;

				string url = string.Format(Constants.TraktTvURLs.URLGetActivityFriends, Constants.TraktTvURLs.APIKey);
				if (shoutsOnly)
					url = string.Format(Constants.TraktTvURLs.URLGetActivityFriendsShoutsOnly, Constants.TraktTvURLs.APIKey);
				logger.Trace("GetActivityFriends: {0}", url);

				TraktTVPost_GetFriends cmdFriends = new TraktTVPost_GetFriends();
				cmdFriends.Init();

				string json = JSONHelper.Serialize<TraktTVPost_GetFriends>(cmdFriends); // TraktTVPost_GetFriends is really just an auth method
				string jsonResponse = SendData(url, json);
				if (jsonResponse.Trim().Length == 0) return null;

				summ = JSONHelper.Deserialize<TraktTV_ActivitySummary>(jsonResponse);
				if (summ == null) return null;


				// save any trakt data that we don't have already
				Trakt_ShowRepository repShows = new Trakt_ShowRepository();
				Trakt_EpisodeRepository repEpisodes = new Trakt_EpisodeRepository();
				Trakt_FriendRepository repFriends = new Trakt_FriendRepository();

				foreach (TraktTV_Activity act in summ.activity)
				{
					if (act.user == null) continue;
					TraktTV_UserActivity friend = act.user;

					Trakt_Friend traktFriend = repFriends.GetByUsername(friend.username);
					if (traktFriend == null)
					{
						traktFriend = new Trakt_Friend();
						traktFriend.LastAvatarUpdate = DateTime.Now;
					}

					traktFriend.Populate(friend);
					repFriends.Save(traktFriend);

					if (!string.IsNullOrEmpty(traktFriend.FullImagePath))
					{
						bool fileExists = File.Exists(traktFriend.FullImagePath);
						TimeSpan ts = DateTime.Now - traktFriend.LastAvatarUpdate;

						if (!fileExists || ts.TotalHours > 8)
						{
							traktFriend.LastAvatarUpdate = DateTime.Now;
							CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(traktFriend.Trakt_FriendID, JMMImageType.Trakt_Friend, true);
							cmd.Save();
						}
					}

					if (act.episode != null && act.show != null)
					{
						Trakt_Show show = repShows.GetByTraktID(act.show.TraktID);
						if (show == null)
						{
							show = new Trakt_Show();
							show.Populate(act.show);
							repShows.Save(show);
						}

						Trakt_Episode episode = repEpisodes.GetByShowIDSeasonAndEpisode(show.Trakt_ShowID, int.Parse(act.episode.season), int.Parse(act.episode.number));
						if (episode == null)
							episode = new Trakt_Episode();

						episode.Populate(act.episode, show.Trakt_ShowID);
						repEpisodes.Save(episode);

						if (!string.IsNullOrEmpty(episode.FullImagePath))
						{
							bool fileExists = File.Exists(episode.FullImagePath);
							if (!fileExists)
							{
								CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(episode.Trakt_EpisodeID, JMMImageType.Trakt_Episode, false);
								cmd.Save();
							}
						}
					}

					// a shout on just the show
					if (act.episode == null && act.show != null)
					{
						Trakt_Show show = repShows.GetByTraktID(act.show.TraktID);
						if (show == null)
						{
							show = new Trakt_Show();
							show.Populate(act.show);
							repShows.Save(show);
						}
					}
				}

			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in TraktTVHelper.GetActivityFriends: " + ex.ToString(), ex);
			}

			return summ;
		}

		public static List<TraktTVUser> GetFriends()
		{
			List<TraktTVUser> friends = new List<TraktTVUser>();

			try
			{
				string url = string.Format(Constants.TraktTvURLs.URLGetFriends, Constants.TraktTvURLs.APIKey, ServerSettings.Trakt_Username);
				//string url = string.Format(Constants.TraktTvURLs.URLGetFriends, Constants.TraktTvURLs.APIKey, "lwerndly");
				logger.Trace("GetFriends: {0}", url);

				TraktTVPost_GetFriends cmdFriends = new TraktTVPost_GetFriends();
				cmdFriends.Init();

				string json = JSONHelper.Serialize<TraktTVPost_GetFriends>(cmdFriends);
				string jsonResponse = SendData(url, json);
				if (jsonResponse.Trim().Length == 0) return friends;
				friends = JSONHelper.Deserialize<List<TraktTVUser>>(jsonResponse);

				/*string json = Utils.DownloadWebPage(url);

				if (json.Trim().Length == 0) return null;

				friends = JSONHelper.Deserialize<List<TraktTVUser>>(json);*/

				Trakt_ShowRepository repShows = new Trakt_ShowRepository();
				Trakt_EpisodeRepository repEpisodes = new Trakt_EpisodeRepository();
				Trakt_FriendRepository repFriends = new Trakt_FriendRepository();

				foreach (TraktTVUser friend in friends)
				{
					Trakt_Friend traktFriend = repFriends.GetByUsername(friend.username);
					if (traktFriend == null)
					{
						traktFriend = new Trakt_Friend();
						traktFriend.LastAvatarUpdate = DateTime.Now;
					}

					traktFriend.Populate(friend);
					repFriends.Save(traktFriend);

					if (!string.IsNullOrEmpty(traktFriend.FullImagePath))
					{
						bool fileExists = File.Exists(traktFriend.FullImagePath);
						TimeSpan ts = DateTime.Now - traktFriend.LastAvatarUpdate;

						if (!fileExists || ts.TotalHours > 8)
						{
							traktFriend.LastAvatarUpdate = DateTime.Now;
							CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(traktFriend.Trakt_FriendID, JMMImageType.Trakt_Friend, true);
							cmd.Save();
						}
					}

					foreach (TraktTVWatched wtch in friend.watched)
					{
						if (wtch.episode != null && wtch.show != null)
						{

							Trakt_Show show = repShows.GetByTraktID(wtch.show.TraktID);
							if (show == null)
							{
								show = new Trakt_Show();
								show.Populate(wtch.show);
								repShows.Save(show);
							}

							Trakt_Episode episode = repEpisodes.GetByShowIDSeasonAndEpisode(show.Trakt_ShowID, int.Parse(wtch.episode.season), int.Parse(wtch.episode.number));
							if (episode == null)
								episode = new Trakt_Episode();

							episode.Populate(wtch.episode, show.Trakt_ShowID);
							repEpisodes.Save(episode);

							if (!string.IsNullOrEmpty(episode.FullImagePath))
							{
								bool fileExists = File.Exists(episode.FullImagePath);
								if (!fileExists)
								{
									CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(episode.Trakt_EpisodeID, JMMImageType.Trakt_Episode, false);
									cmd.Save();
								}
							}
						}
					}
				}

				

				//Contract_Trakt_Friend fr = friends[0].ToContract();

			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in TraktTVHelper.GetFriends: " + ex.ToString(), ex);
				return friends;
			}

			return friends;
		}

		public static void SaveExtendedShowInfo(TraktTVShow tvshow)
		{ 
			try
			{
				// save this data to the DB for use later
				Trakt_ImageFanartRepository repFanart = new Trakt_ImageFanartRepository();
				Trakt_ShowRepository repShows = new Trakt_ShowRepository();
				Trakt_Show show = repShows.GetByTraktID(tvshow.TraktID);
				if (show == null)
					show = new Trakt_Show();

				show.Populate(tvshow);
				repShows.Save(show);


				if (tvshow.images != null)
				{
					if (!string.IsNullOrEmpty(tvshow.images.fanart))
					{
						Trakt_ImageFanart fanart = repFanart.GetByShowIDAndSeason(show.Trakt_ShowID, 1);
						if (fanart == null)
						{
							fanart = new Trakt_ImageFanart();
							fanart.Enabled = 0;
						}

						fanart.ImageURL = tvshow.images.fanart;
						fanart.Season = 1;
						fanart.Trakt_ShowID = show.Trakt_ShowID;
						repFanart.Save(fanart);
					}
				}


				// save the seasons
				Trakt_SeasonRepository repSeasons = new Trakt_SeasonRepository();
				Trakt_EpisodeRepository repEpisodes = new Trakt_EpisodeRepository();
				Trakt_ImagePosterRepository repPosters = new Trakt_ImagePosterRepository();

				foreach (TraktTVSeason sea in tvshow.seasons)
				{
					Trakt_Season season = repSeasons.GetByShowIDAndSeason(show.Trakt_ShowID, int.Parse(sea.season));
					if (season == null)
						season = new Trakt_Season();

					season.Season = int.Parse(sea.season);
					season.URL = sea.url;
					season.Trakt_ShowID = show.Trakt_ShowID;
					repSeasons.Save(season);

					if (sea.images != null)
					{
						if (!string.IsNullOrEmpty(sea.images.poster))
						{
							Trakt_ImagePoster poster = repPosters.GetByShowIDAndSeason(show.Trakt_ShowID, season.Season);
							if (poster == null)
							{
								poster = new Trakt_ImagePoster();
								poster.Enabled = 0;
							}

							poster.ImageURL = sea.images.poster;
							poster.Season = season.Season;
							poster.Trakt_ShowID = show.Trakt_ShowID;
							repPosters.Save(poster);
						}
					}

					foreach (TraktTVEpisode ep in sea.episodes)
					{
						Trakt_Episode episode = repEpisodes.GetByShowIDSeasonAndEpisode(show.Trakt_ShowID, int.Parse(ep.season), int.Parse(ep.episode));
						if (episode == null)
							episode = new Trakt_Episode();

						episode.EpisodeImage = ep.screen;
						episode.EpisodeNumber = int.Parse(ep.episode);
						episode.Overview = ep.overview;
						episode.Season = int.Parse(ep.season);
						episode.Title = ep.title;
						episode.URL = ep.url;
						episode.Trakt_ShowID = show.Trakt_ShowID;
						repEpisodes.Save(episode);
					}
				}


			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in TraktTVHelper.SaveExtendedShowInfo: " + ex.ToString(), ex);
			}
		}

		public static void SaveShowInfo(TraktTVShow tvshow)
		{
			try
			{
				// save this data to the DB for use later
				Trakt_ImageFanartRepository repFanart = new Trakt_ImageFanartRepository();
				Trakt_ShowRepository repShows = new Trakt_ShowRepository();
				Trakt_Show show = repShows.GetByTraktID(tvshow.TraktID);
				if (show == null)
					show = new Trakt_Show();

				show.Overview = tvshow.overview;
				show.Title = tvshow.title;
				show.TraktID = tvshow.TraktID;
				if (!string.IsNullOrEmpty(tvshow.tvdb_id)) show.TvDB_ID = int.Parse(tvshow.tvdb_id);
				show.URL = tvshow.url;
				show.Year = tvshow.year;
				repShows.Save(show);

				if (tvshow.images != null)
				{
					if (!string.IsNullOrEmpty(tvshow.images.fanart))
					{
						Trakt_ImageFanart fanart = repFanart.GetByShowIDAndSeason(show.Trakt_ShowID, 1);
						if (fanart == null)
						{
							fanart = new Trakt_ImageFanart();
							fanart.Enabled = 1;
						}

						fanart.ImageURL = tvshow.images.fanart;
						fanart.Season = 1;
						fanart.Trakt_ShowID = show.Trakt_ShowID;
						repFanart.Save(fanart);
					}
				}

			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in TraktTVHelper.SaveExtendedShowInfo: " + ex.ToString(), ex);
			}
		}

		public static TraktTVShow GetShowInfo(int tvDBID)
		{
			return GetShowInfo(tvDBID.ToString());
		}

        public static string LinkAniDBTrakt(int animeID, enEpisodeType aniEpType, int aniEpNumber, string traktID, int seasonNumber, int traktEpNumber, bool excludeFromWebCache)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
               return LinkAniDBTrakt(session, animeID, aniEpType, aniEpNumber, traktID, seasonNumber, traktEpNumber, excludeFromWebCache);
			}
		}

        public static string LinkAniDBTrakt(ISession session, int animeID, enEpisodeType aniEpType, int aniEpNumber, string traktID, int seasonNumber, int traktEpNumber, bool excludeFromWebCache)
        {
            CrossRef_AniDB_TraktV2Repository repCrossRef = new CrossRef_AniDB_TraktV2Repository();
            List<CrossRef_AniDB_TraktV2> xrefTemps = repCrossRef.GetByAnimeIDEpTypeEpNumber(session, animeID, (int)aniEpType, aniEpNumber);
            if (xrefTemps != null && xrefTemps.Count > 0)
            {
                foreach (CrossRef_AniDB_TraktV2 xrefTemp in xrefTemps)
                {
                    // delete the existing one if we are updating
                    TraktTVHelper.RemoveLinkAniDBTrakt(xrefTemp.AnimeID, (enEpisodeType)xrefTemp.AniDBStartEpisodeType, xrefTemp.AniDBStartEpisodeNumber,
                        xrefTemp.TraktID, xrefTemp.TraktSeasonNumber, xrefTemp.TraktStartEpisodeNumber);
                }
            }

            // check if we have this information locally
            // if not download it now
            Trakt_ShowRepository repSeries = new Trakt_ShowRepository();
            Trakt_Show traktShow = repSeries.GetByTraktID(traktID);
            if (traktShow == null)
            {
                // we download the series info here just so that we have the basic info in the
                // database before the queued task runs later
                TraktTVShow tvshow = GetShowInfo(traktID);
            }

            // download and update series info, episode info and episode images
            // will also download fanart, posters and wide banners
            // download fanart, posters
            DownloadAllImages(traktID);

            CrossRef_AniDB_TraktV2 xref = repCrossRef.GetByTraktID(session, traktID, seasonNumber, traktEpNumber, animeID, (int)aniEpType, aniEpNumber);
            if (xref == null)
                xref = new CrossRef_AniDB_TraktV2();

            xref.AnimeID = animeID;
            xref.AniDBStartEpisodeType = (int)aniEpType;
            xref.AniDBStartEpisodeNumber = aniEpNumber;

            xref.TraktID = traktID;
            xref.TraktSeasonNumber = seasonNumber;
            xref.TraktStartEpisodeNumber = traktEpNumber;
            if (traktShow != null)
                xref.TraktTitle = traktShow.Title;

            if (excludeFromWebCache)
                xref.CrossRefSource = (int)CrossRefSource.WebCache;
            else
                xref.CrossRefSource = (int)CrossRefSource.User;

            repCrossRef.Save(xref);

            StatsCache.Instance.UpdateUsingAnime(animeID);

            logger.Trace("Changed trakt association: {0}", animeID);

            if (!excludeFromWebCache && ServerSettings.WebCache_Trakt_Send)
            {
                CommandRequest_WebCacheSendXRefAniDBTrakt req = new CommandRequest_WebCacheSendXRefAniDBTrakt(xref.CrossRef_AniDB_TraktV2ID);
                req.Save();
            }

            return "";
        }

        public static void RemoveLinkAniDBTrakt(int animeID, enEpisodeType aniEpType, int aniEpNumber, string traktID, int seasonNumber, int traktEpNumber)
        {
            CrossRef_AniDB_TraktV2Repository repCrossRef = new CrossRef_AniDB_TraktV2Repository();
            CrossRef_AniDB_TraktV2 xref = repCrossRef.GetByTraktID(traktID, seasonNumber, traktEpNumber, animeID, (int)aniEpType, aniEpNumber);
            if (xref == null) return;

            repCrossRef.Delete(xref.CrossRef_AniDB_TraktV2ID);

            StatsCache.Instance.UpdateUsingAnime(animeID);

            if (ServerSettings.WebCache_Trakt_Send)
            {
                CommandRequest_WebCacheDeleteXRefAniDBTrakt req = new CommandRequest_WebCacheDeleteXRefAniDBTrakt(animeID, (int)aniEpType, aniEpNumber,
                    traktID, seasonNumber, traktEpNumber);
                req.Save();
            }
        }


		public static List<TraktTVShow> SearchShow(string criteria)
		{
			List<TraktTVShow> results = new List<TraktTVShow>();

			try
			{
				// replace spaces with a + symbo
				criteria = criteria.Replace(' ', '+');

				// Search for a series
				string url = string.Format(Constants.TraktTvURLs.URLSearchShow, Constants.TraktTvURLs.APIKey, criteria);
				logger.Trace("Search Trakt Show: {0}", url);

				// Search for a series
				string json = Utils.DownloadWebPage(url);

				if (json.Trim().Length == 0) return new List<TraktTVShow>();

				results = JSONHelper.Deserialize<List<TraktTVShow>>(json);

				// save this data for later use
				//foreach (TraktTVShow tvshow in results)
				//	SaveExtendedShowInfo(tvshow);
			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in SearchSeries: " + ex.ToString(), ex);
			}

			return results;
		}

		public static List<TraktTVShowUserCollectionWatched> GetUserCollection()
		{
			List<TraktTVShowUserCollectionWatched> results = new List<TraktTVShowUserCollectionWatched>();

			try
			{
				string url = string.Format(Constants.TraktTvURLs.URLUserLibraryShowsCollection, Constants.TraktTvURLs.APIKey, ServerSettings.Trakt_Username);
				logger.Trace("Trakt User Collection: {0}", url);

				// Search for a series
				string json = Utils.DownloadWebPage(url);

				if (json.Trim().Length == 0) return new List<TraktTVShowUserCollectionWatched>();

				results = JSONHelper.Deserialize<List<TraktTVShowUserCollectionWatched>>(json);

			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in SearchSeries: " + ex.ToString(), ex);
			}

			return results;
		}

		public static List<TraktTVShowUserCollectionWatched> GetUserWatched()
		{
			List<TraktTVShowUserCollectionWatched> results = new List<TraktTVShowUserCollectionWatched>();

			try
			{
				string url = string.Format(Constants.TraktTvURLs.URLUserLibraryShowsWatched, Constants.TraktTvURLs.APIKey, ServerSettings.Trakt_Username);
				logger.Trace("Trakt User Collection Watched: {0}", url);

				// Search for a series
				string json = Utils.DownloadWebPage(url);

				if (json.Trim().Length == 0) return new List<TraktTVShowUserCollectionWatched>();

				results = JSONHelper.Deserialize<List<TraktTVShowUserCollectionWatched>>(json);

			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in SearchSeries: " + ex.ToString(), ex);
			}

			return results;
		}

		/// <summary>
		/// Updates the followung
		/// 1. Series Info
		/// 2. Episode Info
		/// 3. Episode Images
		/// 4. Fanart, Poster Images
		/// </summary>
		/// <param name="seriesID"></param>
		/// <param name="forceRefresh"></param>
		public static void UpdateAllInfoAndImages(string traktID, bool forceRefresh)
		{
			// this will do the first 3 steps
			TraktTVShow tvShow = GetShowInfo(traktID);
			if (tvShow == null) return;

			try
			{
				//now download the images
				DownloadAllImages(traktID);
			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in TraktTVHelper.UpdateAllInfoAndImages: " + ex.ToString(), ex);
			}
		}

		public static void DownloadAllImages(string traktID)
		{
			try
			{
				//now download the images
				Trakt_ShowRepository repShow = new Trakt_ShowRepository();
				Trakt_Show show = repShow.GetByTraktID(traktID);
				if (show == null) return;


				if (ServerSettings.Trakt_DownloadFanart)
				{
					//download the fanart image for the show
					Trakt_ImageFanartRepository repFanart = new Trakt_ImageFanartRepository();
					Trakt_ImageFanart fanart = repFanart.GetByShowIDAndSeason(show.Trakt_ShowID, 1);
					if (fanart != null)
					{
						if (!string.IsNullOrEmpty(fanart.FullImagePath))
						{
							if (!File.Exists(fanart.FullImagePath))
							{
								CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(fanart.Trakt_ImageFanartID, JMMImageType.Trakt_Fanart, false);
								cmd.Save();
							}
						}
					}
				}

				
				// download the posters for seasons
				Trakt_ImagePosterRepository repPosters = new Trakt_ImagePosterRepository();
				foreach (Trakt_Season season in show.Seasons)
				{
					if (ServerSettings.Trakt_DownloadPosters)
					{
						Trakt_ImagePoster poster = repPosters.GetByShowIDAndSeason(season.Trakt_ShowID, season.Season);
						if (poster != null)
						{
							if (!string.IsNullOrEmpty(poster.FullImagePath))
							{
								if (!File.Exists(poster.FullImagePath))
								{
									CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(poster.Trakt_ImagePosterID, JMMImageType.Trakt_Poster, false);
									cmd.Save();
								}
							}
						}
					}

					if (ServerSettings.Trakt_DownloadEpisodes)
					{
						// download the screenshots for episodes
						foreach (Trakt_Episode ep in season.Episodes)
						{
							if (!string.IsNullOrEmpty(ep.FullImagePath))
							{
								if (!File.Exists(ep.FullImagePath))
								{
									CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(ep.Trakt_EpisodeID, JMMImageType.Trakt_Episode, false);
									cmd.Save();
								}
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in TraktTVHelper.UpdateAllInfoAndImages: " + ex.ToString(), ex);
			}
		}

		public static void ScanForMatches()
		{
			AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
			List<AnimeSeries> allSeries = repSeries.GetAll();

            CrossRef_AniDB_TraktV2Repository repCrossRef = new CrossRef_AniDB_TraktV2Repository();
			List<CrossRef_AniDB_TraktV2> allCrossRefs = repCrossRef.GetAll();
			List<int> alreadyLinked = new List<int>();
            foreach (CrossRef_AniDB_TraktV2 xref in allCrossRefs)
			{
				alreadyLinked.Add(xref.AnimeID);
			}

			foreach (AnimeSeries ser in allSeries)
			{
				if (alreadyLinked.Contains(ser.AniDB_ID)) continue;

				AniDB_Anime anime = ser.GetAnime();

				if (anime != null)
					logger.Trace("Found anime without Trakt association: " + anime.MainTitle);

				if (anime.IsTraktLinkDisabled) continue;

				CommandRequest_TraktSearchAnime cmd = new CommandRequest_TraktSearchAnime(ser.AniDB_ID, false);
				cmd.Save();
			}

		}

		public static void UpdateAllInfo()
		{
            CrossRef_AniDB_TraktV2Repository repCrossRef = new CrossRef_AniDB_TraktV2Repository();
            List<CrossRef_AniDB_TraktV2> allCrossRefs = repCrossRef.GetAll();
            foreach (CrossRef_AniDB_TraktV2 xref in allCrossRefs)
			{
				CommandRequest_TraktUpdateInfoAndImages cmd = new CommandRequest_TraktUpdateInfoAndImages(xref.TraktID);
				cmd.Save();
			}

		}


        public static void ScrobbleEpisode(bool watched, Trakt_Show show, int season, int episodeNumber, AniDB_Episode aniep)
        {
            try
            {
                string url = string.Empty;
                string json = string.Empty;
                if (watched)
                {
                    TraktTVPost_ShowScrobble postScrobble = new TraktTVPost_ShowScrobble();
                    postScrobble.SetCredentials();
                    postScrobble.imdb_id = "";
                    postScrobble.title = show.Title;
                    postScrobble.year = show.Year;
                    postScrobble.tvdb_id = show.TvDB_ID.Value.ToString();
                    postScrobble.episode = episodeNumber.ToString();
                    postScrobble.season = season.ToString();

                    TimeSpan t = TimeSpan.FromSeconds(aniep.LengthSeconds + 14);
                    int toMinutes = int.Parse(Math.Round(t.TotalMinutes).ToString());
                    postScrobble.duration = toMinutes.ToString();

                    postScrobble.progress = "100";

                    postScrobble.plugin_version = "0.4";
                    postScrobble.media_center_version = "1.2.0.1";
                    postScrobble.media_center_date = "Dec 17 2010";

                    logger.Trace("Marking episode as watched (scrobble) on Trakt: {0} - S{1} - EP{2}", show.Title, season, episodeNumber);

                    url = string.Format(Constants.TraktTvURLs.URLPostShowScrobble, Constants.TraktTvURLs.APIKey);
                    json = JSONHelper.Serialize<TraktTVPost_ShowScrobble>(postScrobble);
                }
                else
                {

                    TraktTVPost_ShowEpisodeUnseen postUnseen = new TraktTVPost_ShowEpisodeUnseen();
                    postUnseen.episodes = new List<TraktTVSeasonEpisode>();
                    postUnseen.SetCredentials();
                    postUnseen.imdb_id = "";
                    postUnseen.title = show.Title;
                    postUnseen.year = show.Year;
                    postUnseen.tvdb_id = show.TvDB_ID.Value.ToString();

                    TraktTVSeasonEpisode traktEp = new TraktTVSeasonEpisode();
                    traktEp.episode = episodeNumber.ToString();
                    traktEp.season = season.ToString();
                    postUnseen.episodes.Add(traktEp);

                    logger.Trace("Marking episode as unwatched on Trakt: {0} - S{1} - EP{2}", show.Title, season, episodeNumber);

                    url = string.Format(Constants.TraktTvURLs.URLPostShowEpisodeUnseen, Constants.TraktTvURLs.APIKey);
                    json = JSONHelper.Serialize<TraktTVPost_ShowEpisodeUnseen>(postUnseen);

                }

                SendData(url, json);

            }
            catch (Exception ex)
            {
                logger.ErrorException("Error in TraktTVHelper.MarkEpisodeWatched: " + ex.ToString(), ex);
            }
        }

		public static void MarkEpisodeWatched(AnimeEpisode ep)
		{
            try
			{
				if (string.IsNullOrEmpty(ServerSettings.Trakt_Username) || string.IsNullOrEmpty(ServerSettings.Trakt_Password))
					return;

				AniDB_Episode aniep = ep.AniDB_Episode;
                if (aniep == null) return;

                AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
                AniDB_Anime anime = repAnime.GetByAnimeID(aniep.AnimeID);
                if (anime == null) return;

                string traktID = string.Empty;
                int retEpNum = -1;
				int retSeason = -1;

                GetTraktEpisodeNumber(anime, aniep, ref traktID, ref retEpNum, ref retSeason);
				if (retEpNum < 0) return;

				Trakt_ShowRepository repShows = new Trakt_ShowRepository();
				Trakt_Show show = repShows.GetByTraktID(traktID);
				if (show == null) return;
				if (!show.TvDB_ID.HasValue) return;

                ScrobbleEpisode(true, show, retSeason, retEpNum, aniep);

			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in TraktTVHelper.MarkEpisodeWatched: " + ex.ToString(), ex);
			}

		}

		public static void MarkEpisodeUnwatched(AnimeEpisode ep)
		{
			try
			{
                if (string.IsNullOrEmpty(ServerSettings.Trakt_Username) || string.IsNullOrEmpty(ServerSettings.Trakt_Password))
                    return;

                AniDB_Episode aniep = ep.AniDB_Episode;
                if (aniep == null) return;

                AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
                AniDB_Anime anime = repAnime.GetByAnimeID(aniep.AnimeID);
                if (anime == null) return;

                string traktID = string.Empty;
                int retEpNum = -1;
                int retSeason = -1;

                GetTraktEpisodeNumber(anime, aniep, ref traktID, ref retEpNum, ref retSeason);
                if (retEpNum < 0) return;

				Trakt_ShowRepository repShows = new Trakt_ShowRepository();
                Trakt_Show show = repShows.GetByTraktID(traktID);
				if (show == null) return;
				if (!show.TvDB_ID.HasValue) return;

                ScrobbleEpisode(false, show, retSeason, retEpNum, aniep);

			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in TraktTVHelper.MarkEpisodeWatched: " + ex.ToString(), ex);
			}

		}


		private static string SendData(string uri, string json)
		{
			WebRequest req = null;
			WebResponse rsp = null;
			string output = "";
			try
			{
				DateTime start = DateTime.Now;


				req = WebRequest.Create(uri);
				req.Method = "POST";        // Post method
				req.ContentType = "text/json";     // content type
				req.Proxy = null;

				// Wrap the request stream with a text-based writer
				StreamWriter writer = new StreamWriter(req.GetRequestStream());
				// Write the XML text into the stream
				writer.WriteLine(json);
				writer.Close();
				// Send the data to the webserver
				//rsp = req.GetResponse();

				HttpWebResponse WebResponse = (HttpWebResponse)req.GetResponse();

				Stream responseStream = WebResponse.GetResponseStream();
				String enco = WebResponse.CharacterSet;
				Encoding encoding = null;
				if (!String.IsNullOrEmpty(enco))
					encoding = Encoding.GetEncoding(WebResponse.CharacterSet);
				if (encoding == null)
					encoding = Encoding.Default;
				StreamReader Reader = new StreamReader(responseStream, encoding);

				output = Reader.ReadToEnd();


				TimeSpan ts = DateTime.Now - start;
				logger.Trace("Sent TraktPost in {0} ms: {1} --- {2}", ts.TotalMilliseconds, uri, output);

			}
			catch (WebException webEx)
			{
				logger.Error("Error(1) in XMLServiceQueue.SendData: {0}", webEx);
			}
			catch (Exception ex)
			{
				logger.ErrorException("Error(2) in XMLServiceQueue.SendData: {0}", ex);
			}
			finally
			{
				if (req != null) req.GetRequestStream().Close();
				if (rsp != null) rsp.GetResponseStream().Close();
			}

			return output;
		}

		public static string TestUserLogin()
		{
			try
			{
				if (string.IsNullOrEmpty(ServerSettings.Trakt_Username) || string.IsNullOrEmpty(ServerSettings.Trakt_Password))
					return "Please enter a username and password";

				TraktTVPost_AccountTest cmd = new TraktTVPost_AccountTest();
				cmd.Init();

				string url = string.Format(Constants.TraktTvURLs.URLPostAccountTest, Constants.TraktTvURLs.APIKey);
				logger.Trace("TestUserLogin: {0}", url);

				string json = JSONHelper.Serialize<TraktTVPost_AccountTest>(cmd);
				string jsonResponse = SendData(url, json);
				if (string.IsNullOrEmpty(jsonResponse)) return "Invalid login";

				TraktTVGenericResponse genResponse = JSONHelper.Deserialize<TraktTVGenericResponse>(jsonResponse);
				if (genResponse.IsSuccess)
				{
					MainWindow.UpdateTraktFriendInfo(true);
					return "";
				}
				else
					return genResponse.error;

			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in TraktTVHelper.TestUserLogin: " + ex.ToString(), ex);
				return ex.Message;
			}
		}

		public static bool CreateAccount(string uname, string pass, string emailAddress, ref string returnMessage)
		{
			returnMessage = "";
			try
			{
				if (string.IsNullOrEmpty(uname) || string.IsNullOrEmpty(pass) || string.IsNullOrEmpty(emailAddress))
				{
					returnMessage = "Please enter a username and password";
					return false;
				}

				TraktTVPost_AccountCreate cmd = new TraktTVPost_AccountCreate();
				cmd.Init(uname, pass, emailAddress);

				string url = string.Format(Constants.TraktTvURLs.URLPostAccountCreate, Constants.TraktTvURLs.APIKey);
				logger.Trace("CreateAccount: {0}", url);

				string json = JSONHelper.Serialize<TraktTVPost_AccountCreate>(cmd);
				string jsonResponse = SendData(url, json);

				TraktTVGenericResponse genResponse = JSONHelper.Deserialize<TraktTVGenericResponse>(jsonResponse);
				if (genResponse.IsSuccess)
				{
					returnMessage = genResponse.message;
					ServerSettings.Trakt_Username = uname;
					ServerSettings.Trakt_Password = pass;
					MainWindow.UpdateTraktFriendInfo(true);
					return true;
				}
				else
				{
					returnMessage = genResponse.error;
					return false;
				}

			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in TraktTVHelper.TestUserLogin: " + ex.ToString(), ex);
				returnMessage = ex.Message;
				return false;
			}
		}

		public static bool FriendRequestDeny(string uname, ref string returnMessage)
		{
			returnMessage = "";
			try
			{
				if (string.IsNullOrEmpty(ServerSettings.Trakt_Username) || string.IsNullOrEmpty(ServerSettings.Trakt_Password))
				{
					returnMessage = "Please enter a username and password";
					return false;
				}

				TraktTVPost_FriendDenyApprove cmd = new TraktTVPost_FriendDenyApprove();
				cmd.Init(uname);

				string url = string.Format(Constants.TraktTvURLs.URLPostFriendsDeny, Constants.TraktTvURLs.APIKey);
				logger.Trace("URLPostFriendsDeny: {0}", url);

				string json = JSONHelper.Serialize<TraktTVPost_FriendDenyApprove>(cmd);
				string jsonResponse = SendData(url, json);
				if (string.IsNullOrEmpty(jsonResponse))
				{
					returnMessage = "Error occurred";
					return false;
				}

				TraktTVGenericResponse genResponse = JSONHelper.Deserialize<TraktTVGenericResponse>(jsonResponse);
				if (genResponse.IsSuccess)
				{
					returnMessage = genResponse.message;
					MainWindow.UpdateTraktFriendInfo(true);
					return true;
				}
				else
				{
					returnMessage = genResponse.error;
					return false;
				}

			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in TraktTVHelper.FriendRequestDeny: " + ex.ToString(), ex);
				returnMessage = ex.Message;
				return false;
			}
		}

		public static bool FriendRequestApprove(string uname, ref string returnMessage)
		{
			returnMessage = "";
			try
			{
				if (string.IsNullOrEmpty(ServerSettings.Trakt_Username) || string.IsNullOrEmpty(ServerSettings.Trakt_Password))
				{
					returnMessage = "Please enter a username and password";
					return false;
				}

				TraktTVPost_FriendDenyApprove cmd = new TraktTVPost_FriendDenyApprove();
				cmd.Init(uname);

				string url = string.Format(Constants.TraktTvURLs.URLPostFriendsApprove, Constants.TraktTvURLs.APIKey);
				logger.Trace("URLPostFriendsDeny: {0}", url);

				string json = JSONHelper.Serialize<TraktTVPost_FriendDenyApprove>(cmd);
				string jsonResponse = SendData(url, json);
				if (string.IsNullOrEmpty(jsonResponse))
				{
					returnMessage = "Error occurred";
					return false;
				}

				TraktTVGenericResponse genResponse = JSONHelper.Deserialize<TraktTVGenericResponse>(jsonResponse);
				if (genResponse.IsSuccess)
				{
					returnMessage = genResponse.message;
					MainWindow.UpdateTraktFriendInfo(true);
					return true;
				}
				else
				{
					returnMessage = genResponse.error;
					return false;
				}

			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in TraktTVHelper.FriendRequestDeny: " + ex.ToString(), ex);
				returnMessage = ex.Message;
				return false;
			}
		}

		public static void SyncCollectionToTrakt_Series(AnimeSeries series)
		{
			try
			{
				// check that we have at least one user nominated for Trakt
				JMMUserRepository repUsers = new JMMUserRepository();
				List<JMMUser> traktUsers = repUsers.GetTraktUsers();
				if (traktUsers.Count == 0) return;

				string url = string.Format(Constants.TraktTvURLs.URLPostShowEpisodeLibrary, Constants.TraktTvURLs.APIKey);
				string urlSeen = string.Format(Constants.TraktTvURLs.URLPostShowEpisodeSeen, Constants.TraktTvURLs.APIKey);

                AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
                AniDB_Anime anime = repAnime.GetByAnimeID(series.AniDB_ID);
                if (anime == null) return;

                TraktSummaryContainer traktSummary = new TraktSummaryContainer();
                traktSummary.Populate(series.AniDB_ID);

				

                Dictionary<string, TraktTVPost_ShowEpisodeLibrary> postLibraries = new Dictionary<string, TraktTVPost_ShowEpisodeLibrary>();
                Dictionary<string, TraktTVPost_ShowEpisodeSeen> postSeens = new Dictionary<string, TraktTVPost_ShowEpisodeSeen>();

                
                Trakt_ShowRepository repShows = new Trakt_ShowRepository();

				foreach (AnimeEpisode ep in series.GetAnimeEpisodes())
				{
					if (ep.GetVideoLocals().Count > 0)
					{
                        AniDB_Episode aniep = ep.AniDB_Episode;
                        if (aniep == null) return;

                        string traktID = string.Empty;
                        int retEpNum = -1;
                        int retSeason = -1;

                        GetTraktEpisodeNumber(anime, aniep, ref traktID, ref retEpNum, ref retSeason);
                        if (retEpNum < 0) continue;

                        if (!traktSummary.TraktDetails.ContainsKey(traktID)) continue;

                        Trakt_Show show = traktSummary.TraktDetails[traktID].Show;
                        if (show == null) continue;
                        if (!show.TvDB_ID.HasValue) continue;

						TraktTVSeasonEpisode traktEp = new TraktTVSeasonEpisode();
						traktEp.episode = retEpNum.ToString();
						traktEp.season = retSeason.ToString();

                        if (!postLibraries.ContainsKey(traktID))
                        {
                            postLibraries[traktID] = new TraktTVPost_ShowEpisodeLibrary();
                            postLibraries[traktID].episodes = new List<TraktTVSeasonEpisode>();
                            postLibraries[traktID].SetCredentials();
                            postLibraries[traktID].imdb_id = "";
                            postLibraries[traktID].title = show.Title;
                            postLibraries[traktID].year = show.Year;
                            postLibraries[traktID].tvdb_id = show.TvDB_ID.Value.ToString();
                        }

                        postLibraries[traktID].episodes.Add(traktEp);

						AnimeEpisode_User userRecord = null;
						foreach (JMMUser juser in traktUsers)
						{
							userRecord = ep.GetUserRecord(juser.JMMUserID);
							if (userRecord != null) break;
						}

						if (userRecord != null)
                        {
                            if (!postSeens.ContainsKey(traktID))
                            {
                                postSeens[traktID] = new TraktTVPost_ShowEpisodeSeen();
                                postSeens[traktID].episodes = new List<TraktTVSeasonEpisode>();
                                postSeens[traktID].SetCredentials();
                                postSeens[traktID].imdb_id = "";
                                postSeens[traktID].title = show.Title;
                                postSeens[traktID].year = show.Year;
                                postSeens[traktID].tvdb_id = show.TvDB_ID.Value.ToString();
                            }

                            postSeens[traktID].episodes.Add(traktEp);
                        }
							
					}
				}

                foreach (TraktTVPost_ShowEpisodeLibrary postLibrary in postLibraries.Values)
                {
                    if (postLibrary.episodes.Count > 0)
                    {
                        logger.Info("PostShowEpisodeLibrary: {0}/{1}/{2} eps", postLibrary.title, postLibrary.tvdb_id, postLibrary.episodes.Count);

                        string json = JSONHelper.Serialize<TraktTVPost_ShowEpisodeLibrary>(postLibrary);
                        string jsonResponse = SendData(url, json);
                        logger.Info("PostShowEpisodeLibrary RESPONSE: {0}", jsonResponse);
                    }
                }

                foreach (TraktTVPost_ShowEpisodeSeen postSeen in postSeens.Values)
                {
                    if (postSeen.episodes.Count > 0)
                    {
                        logger.Info("PostShowEpisodeSeen: {0}/{1}/{2} eps", postSeen.title, postSeen.tvdb_id, postSeen.episodes.Count);

                        string json = JSONHelper.Serialize<TraktTVPost_ShowEpisodeSeen>(postSeen);
                        string jsonResponse = SendData(urlSeen, json);
                        logger.Info("PostShowEpisodeSeen RESPONSE: {0}", jsonResponse);
                    }
                }
				
			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in TraktTVHelper.SyncCollectionToTrakt_Series: " + ex.ToString(), ex);
			}
		}

		public static void SyncCollectionToTrakt()
		{
			try
			{
				if (string.IsNullOrEmpty(ServerSettings.Trakt_Username) || string.IsNullOrEmpty(ServerSettings.Trakt_Password)) return;

				AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
				List<AnimeSeries> allSeries = repSeries.GetAll();

				foreach (AnimeSeries series in allSeries)
				{
					//SyncCollectionToTrakt_Series(series);
					CommandRequest_TraktSyncCollectionSeries cmd = new CommandRequest_TraktSyncCollectionSeries(series.AnimeSeriesID, series.GetAnime().MainTitle);
					cmd.Save();
				}

			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in TraktTVHelper.SyncCollectionToTrakt: " + ex.ToString(), ex);
			}
		}

		private static void GetTraktEpisodeNumber(AniDB_Anime anime, AniDB_Episode ep, ref string traktID, ref int traktEpNum, ref int traktSeason)
		{
            TraktSummaryContainer traktSummary = new TraktSummaryContainer();
            traktSummary.Populate(anime.AnimeID);

            GetTraktEpisodeNumber(traktSummary, anime, ep, ref traktID, ref  traktEpNum, ref  traktSeason);

		}

        private static void GetTraktEpisodeNumber(TraktSummaryContainer traktSummary, AniDB_Anime anime, AniDB_Episode ep, ref string traktID, ref int traktEpNum, ref int traktSeason)
		{
			try
			{
                traktEpNum = -1;
                traktSeason = -1;
                traktID = string.Empty;

                #region normal episodes
                // now do stuff to improve performance
                if (ep.EpisodeTypeEnum == enEpisodeType.Episode)
                {
                    if (traktSummary != null && traktSummary.CrossRefTraktV2 != null && traktSummary.CrossRefTraktV2.Count > 0)
                    {
                        // find the xref that is right
                        // relies on the xref's being sorted by season number and then episode number (desc)
                        List<SortPropOrFieldAndDirection> sortCriteria = new List<SortPropOrFieldAndDirection>();
                        sortCriteria.Add(new SortPropOrFieldAndDirection("AniDBStartEpisodeNumber", true, SortType.eInteger));
                        List<CrossRef_AniDB_TraktV2> traktCrossRef = Sorting.MultiSort<CrossRef_AniDB_TraktV2>(traktSummary.CrossRefTraktV2, sortCriteria);

                        bool foundStartingPoint = false;
                        CrossRef_AniDB_TraktV2 xrefBase = null;
                        foreach (CrossRef_AniDB_TraktV2 xrefTrakt in traktCrossRef)
                        {
                            if (xrefTrakt.AniDBStartEpisodeType != (int)enEpisodeType.Episode) continue;
                            if (ep.EpisodeNumber >= xrefTrakt.AniDBStartEpisodeNumber)
                            {
                                foundStartingPoint = true;
                                xrefBase = xrefTrakt;
                                break;
                            }
                        }

                        // we have found the starting epiosde numbder from AniDB
                        // now let's check that the Trakt Season and Episode Number exist
                        if (foundStartingPoint)
                        {

                            Dictionary<int, int> dictTraktSeasons = null;
                            Dictionary<int, Trakt_Episode> dictTraktEpisodes = null;
                            foreach (TraktDetailsContainer det in traktSummary.TraktDetails.Values)
                            {
                                if (det.TraktID == xrefBase.TraktID)
                                {
                                    dictTraktSeasons = det.DictTraktSeasons;
                                    dictTraktEpisodes = det.DictTraktEpisodes;
                                    break;
                                }
                            }

                            if (dictTraktSeasons.ContainsKey(xrefBase.TraktSeasonNumber))
                            {
                                int episodeNumber = dictTraktSeasons[xrefBase.TraktSeasonNumber] + (ep.EpisodeNumber + xrefBase.TraktStartEpisodeNumber - 2) - 
                                    (xrefBase.AniDBStartEpisodeNumber - 1);
                                if (dictTraktEpisodes.ContainsKey(episodeNumber))
                                {
                                    Trakt_Episode traktep = dictTraktEpisodes[episodeNumber];

                                    traktEpNum = traktep.EpisodeNumber;
                                    traktSeason = xrefBase.TraktSeasonNumber;
                                    traktID = xrefBase.TraktID;
                                }
                            }
                        }
                    }
                }
                #endregion


                #region special episodes
                if (ep.EpisodeTypeEnum == enEpisodeType.Special)
                {
                    // find the xref that is right
                    // relies on the xref's being sorted by season number and then episode number (desc)
                    List<SortPropOrFieldAndDirection> sortCriteria = new List<SortPropOrFieldAndDirection>();
                    sortCriteria.Add(new SortPropOrFieldAndDirection("AniDBStartEpisodeNumber", true, SortType.eInteger));
                    List<CrossRef_AniDB_TraktV2> traktCrossRef = Sorting.MultiSort<CrossRef_AniDB_TraktV2>(traktSummary.CrossRefTraktV2, sortCriteria);

                    bool foundStartingPoint = false;
                    CrossRef_AniDB_TraktV2 xrefBase = null;
                    foreach (CrossRef_AniDB_TraktV2 xrefTrakt in traktCrossRef)
                    {
                        if (xrefTrakt.AniDBStartEpisodeType != (int)enEpisodeType.Special) continue;
                        if (ep.EpisodeNumber >= xrefTrakt.AniDBStartEpisodeNumber)
                        {
                            foundStartingPoint = true;
                            xrefBase = xrefTrakt;
                            break;
                        }
                    }

                    if (traktSummary != null && traktSummary.CrossRefTraktV2 != null && traktSummary.CrossRefTraktV2.Count > 0)
                    {
                        // we have found the starting epiosde numbder from AniDB
                        // now let's check that the Trakt Season and Episode Number exist
                        if (foundStartingPoint)
                        {

                            Dictionary<int, int> dictTraktSeasons = null;
                            Dictionary<int, Trakt_Episode> dictTraktEpisodes = null;
                            foreach (TraktDetailsContainer det in traktSummary.TraktDetails.Values)
                            {
                                if (det.TraktID == xrefBase.TraktID)
                                {
                                    dictTraktSeasons = det.DictTraktSeasons;
                                    dictTraktEpisodes = det.DictTraktEpisodes;
                                    break;
                                }
                            }

                            if (dictTraktSeasons.ContainsKey(xrefBase.TraktSeasonNumber))
                            {
                                int episodeNumber = dictTraktSeasons[xrefBase.TraktSeasonNumber] + (ep.EpisodeNumber + xrefBase.TraktStartEpisodeNumber - 2) - 
                                    (xrefBase.AniDBStartEpisodeNumber - 1);
                                if (dictTraktEpisodes.ContainsKey(episodeNumber))
                                {
                                    Trakt_Episode traktep = dictTraktEpisodes[episodeNumber];

                                    traktEpNum = traktep.EpisodeNumber;
                                    traktSeason = xrefBase.TraktSeasonNumber;
                                    traktID = xrefBase.TraktID;
                                }
                            }
                        }
                    }
                }
                #endregion

				return;
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
				return;
			}
		}

		private static void GetDictTraktEpisodesAndSeasons(Trakt_Show show, ref Dictionary<int, Trakt_Episode> dictTraktEpisodes, 
			ref Dictionary<int, Trakt_Episode> dictTraktSpecials, ref Dictionary<int, int> dictTraktSeasons)
		{
			dictTraktEpisodes = new Dictionary<int, Trakt_Episode>();
			dictTraktSpecials = new Dictionary<int, Trakt_Episode>();
			dictTraktSeasons = new Dictionary<int, int>();
			try
			{
				Trakt_EpisodeRepository repEps = new Trakt_EpisodeRepository();

				// create a dictionary of absolute episode numbers for trakt episodes
				// sort by season and episode number
				// ignore season 0, which is used for specials
				List<Trakt_Episode> eps = repEps.GetByShowID(show.Trakt_ShowID);

				List<SortPropOrFieldAndDirection> sortCriteria = new List<SortPropOrFieldAndDirection>();
				sortCriteria.Add(new SortPropOrFieldAndDirection("Season", false, SortType.eInteger));
				sortCriteria.Add(new SortPropOrFieldAndDirection("EpisodeNumber", false, SortType.eInteger));
				eps = Sorting.MultiSort<Trakt_Episode>(eps, sortCriteria);

				int i = 1;
				int iSpec = 1;
				int lastSeason = -999;
				foreach (Trakt_Episode ep in eps)
				{
					//if (ep.Season == 0) continue;
					if (ep.Season > 0)
					{
						dictTraktEpisodes[i] = ep;
						if (ep.Season != lastSeason)
							dictTraktSeasons[ep.Season] = i;

						i++;
					}
					else
					{
						dictTraktSpecials[iSpec] = ep;
						if (ep.Season != lastSeason)
							dictTraktSeasons[ep.Season] = iSpec;

						iSpec++;
					}

					lastSeason = ep.Season;
				}
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
			}
		}
	}
}

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

		public static List<TraktTV_ShoutGet> GetShowShouts(int animeID)
		{
			List<TraktTV_ShoutGet> shouts = null;
			try
			{
				if (string.IsNullOrEmpty(ServerSettings.Trakt_Username) || string.IsNullOrEmpty(ServerSettings.Trakt_Password))
					return null;

				CrossRef_AniDB_TraktRepository repXrefTrakt = new CrossRef_AniDB_TraktRepository();
				CrossRef_AniDB_Trakt traktXRef = repXrefTrakt.GetByAnimeID(animeID);
				if (traktXRef == null) return null;

				string url = string.Format(Constants.TraktTvURLs.URLGetShowShouts, Constants.TraktTvURLs.APIKey, traktXRef.TraktID);
				logger.Trace("GetShowShouts: {0}", url);

				// Search for a series
				string json = Utils.DownloadWebPage(url);

				if (json.Trim().Length == 0) return new List<TraktTV_ShoutGet>();

				shouts = JSONHelper.Deserialize<List<TraktTV_ShoutGet>>(json);

			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in TraktTVHelper.GetShowShouts: " + ex.ToString(), ex);
			}

			return shouts;
		}

		public static TraktTV_ActivitySummary GetActivityFriends()
		{
			TraktTV_ActivitySummary summ = null;
			try
			{
				if (string.IsNullOrEmpty(ServerSettings.Trakt_Username) || string.IsNullOrEmpty(ServerSettings.Trakt_Password))
					return null;

				string url = string.Format(Constants.TraktTvURLs.URLGetActivityFriends, Constants.TraktTvURLs.APIKey);
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
							fanart.Enabled = 1;
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
								poster.Enabled = 1;
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

		public static void LinkAniDBTrakt(int animeID, string traktID, int seasonNumber, bool fromWebCache)
		{
			CrossRef_AniDB_TraktRepository repCrossRef = new CrossRef_AniDB_TraktRepository();
			CrossRef_AniDB_Trakt xrefTemp = repCrossRef.GetByTraktID(traktID, seasonNumber);
			if (xrefTemp != null)
			{
				string msg = string.Format("Not using Trakt link as one already exists {0} ({1}) - {2}", traktID, seasonNumber, animeID);
				logger.Warn(msg);
				return;
			}

			// check if we have this information locally
			// if not download it now
			Trakt_ShowRepository repShow = new Trakt_ShowRepository();
			Trakt_Show traktShow = repShow.GetByTraktID(traktID);
			if (traktShow == null)
			{
				// we download the series info here
				TraktTVShow tvshow = GetShowInfo(traktID);
				if (tvshow == null) return;
			}

			// download fanart, posters
			DownloadAllImages(traktID);

			CrossRef_AniDB_Trakt xref = repCrossRef.GetByAnimeID(animeID);
			if (xref == null)
				xref = new CrossRef_AniDB_Trakt();

			xref.AnimeID = animeID;
			if (fromWebCache)
				xref.CrossRefSource = (int)CrossRefSource.WebCache;
			else
				xref.CrossRefSource = (int)CrossRefSource.User;

			xref.TraktID = traktID;
			xref.TraktSeasonNumber = seasonNumber;
			repCrossRef.Save(xref);

			StatsCache.Instance.UpdateUsingAnime(animeID);

			logger.Trace("Changed trakt association: {0}", animeID);

			CommandRequest_WebCacheSendXRefAniDBTrakt req = new CommandRequest_WebCacheSendXRefAniDBTrakt(xref.CrossRef_AniDB_TraktID);
			req.Save();
		}

		// Removes all Trakt information from a series, bringing it back to a blank state.
		public static void RemoveLinkAniDBTrakt(AnimeSeries ser)
		{
			CrossRef_AniDB_TraktRepository repCrossRef = new CrossRef_AniDB_TraktRepository();
			CrossRef_AniDB_Trakt xref = repCrossRef.GetByAnimeID(ser.AniDB_ID);
			if (xref == null) return;

			repCrossRef.Delete(xref.CrossRef_AniDB_TraktID);

			CommandRequest_WebCacheDeleteXRefAniDBTrakt req = new CommandRequest_WebCacheDeleteXRefAniDBTrakt(ser.AniDB_ID);
			req.Save();
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
				//foreach (TraktTVShowResponse tvshow in results)
				//	SaveShowInfo(tvshow);
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

				// download the posters for seasons
				Trakt_ImagePosterRepository repPosters = new Trakt_ImagePosterRepository();
				foreach (Trakt_Season season in show.Seasons)
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
			catch (Exception ex)
			{
				logger.ErrorException("Error in TraktTVHelper.UpdateAllInfoAndImages: " + ex.ToString(), ex);
			}
		}

		public static void ScanForMatches()
		{
			AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
			List<AnimeSeries> allSeries = repSeries.GetAll();

			CrossRef_AniDB_TraktRepository repCrossRef = new CrossRef_AniDB_TraktRepository();
			List<CrossRef_AniDB_Trakt> allCrossRefs = repCrossRef.GetAll();
			List<int> alreadyLinked = new List<int>();
			foreach (CrossRef_AniDB_Trakt xref in allCrossRefs)
			{
				alreadyLinked.Add(xref.AnimeID);
			}

			foreach (AnimeSeries ser in allSeries)
			{
				if (alreadyLinked.Contains(ser.AniDB_ID)) continue;

				AniDB_Anime anime = ser.Anime;

				if (anime != null)
					logger.Trace("Found anime without Trakt association: " + anime.MainTitle);

				CommandRequest_TraktSearchAnime cmd = new CommandRequest_TraktSearchAnime(ser.AniDB_ID, false);
				cmd.Save();
			}

		}

		public static void UpdateAllInfo()
		{
			CrossRef_AniDB_TraktRepository repCrossRef = new CrossRef_AniDB_TraktRepository();
			List<CrossRef_AniDB_Trakt> allCrossRefs = repCrossRef.GetAll();
			foreach (CrossRef_AniDB_Trakt xref in allCrossRefs)
			{
				CommandRequest_TraktUpdateInfoAndImages cmd = new CommandRequest_TraktUpdateInfoAndImages(xref.TraktID);
				cmd.Save();
			}

		}

		/*public static void MarkEpisodeWatched(AnimeEpisode ep)
		{
			TraktTVPost_ShowScrobble tt = new TraktTVPost_ShowScrobble();
			if (!tt.Init(ep)) return;

			try
			{
				string url = string.Format(Constants.TraktTvURLs.URLPostShowScrobble, Constants.TraktTvURLs.APIKey);
				logger.Trace("GetShowInfo: {0}", url);

				logger.Trace("Marking episode as unwatched on Trakt: {0} - S{1} - EP{2}", show.Title, retSeason, retEpNum);

				string json = JSONHelper.Serialize<TraktTVPost_ShowScrobble>(tt);

				SendData(url, json);

			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in TraktTVHelper.MarkEpisodeWatched: " + ex.ToString(), ex);
			}

		}*/

		public static void MarkEpisodeWatched(AnimeEpisode ep)
		{
			try
			{

				CrossRef_AniDB_Trakt xref = ep.AnimeSeries.CrossRefTrakt;
				if (xref == null) return;

				Trakt_ShowRepository repShows = new Trakt_ShowRepository();
				Trakt_Show show = repShows.GetByTraktID(xref.TraktID);
				if (show == null) return;
				if (!show.TvDB_ID.HasValue) return;

				Dictionary<int, int> dictTraktSeasons = null;
				Dictionary<int, Trakt_Episode> dictTraktEpisodes = null;
				Dictionary<int, Trakt_Episode> dictTraktSpecials = null;
				GetDictTraktEpisodesAndSeasons(show, ref dictTraktEpisodes, ref dictTraktSpecials, ref dictTraktSeasons);

				int retEpNum = -1;
				int retSeason = -1;

				GetTraktEpisodeNumber(ep, ep.AnimeSeries, show, xref.TraktSeasonNumber, ref retEpNum, ref retSeason, dictTraktEpisodes, dictTraktSpecials, dictTraktSeasons);
				if (retEpNum < 0) return;

				TraktTVPost_ShowScrobble postScrobble = new TraktTVPost_ShowScrobble();
				postScrobble.SetCredentials();
				postScrobble.imdb_id = "";
				postScrobble.title = show.Title;
				postScrobble.year = show.Year;
				postScrobble.tvdb_id = show.TvDB_ID.Value.ToString();
				postScrobble.episode = retEpNum.ToString();
				postScrobble.season = retSeason.ToString();

				AniDB_Episode aniep = ep.AniDB_Episode;
				if (aniep != null)
				{
					TimeSpan t = TimeSpan.FromSeconds(aniep.LengthSeconds + 14);
					int toMinutes = int.Parse(Math.Round(t.TotalMinutes).ToString());
					postScrobble.duration = toMinutes.ToString();
				}
				else
					postScrobble.duration = "25";

				postScrobble.progress = "100";

				postScrobble.plugin_version = "0.4";
				postScrobble.media_center_version = "1.2.0.1";
				postScrobble.media_center_date = "Dec 17 2010";
			
				logger.Trace("Marking episode as watched (scrobble) on Trakt: {0} - S{1} - EP{2}", show.Title, retSeason, retEpNum);

				string url = string.Format(Constants.TraktTvURLs.URLPostShowScrobble, Constants.TraktTvURLs.APIKey);
				string json = JSONHelper.Serialize<TraktTVPost_ShowScrobble>(postScrobble);

				SendData(url, json);

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
				
				CrossRef_AniDB_Trakt xref = ep.AnimeSeries.CrossRefTrakt;
				if (xref == null) return;

				Trakt_ShowRepository repShows = new Trakt_ShowRepository();
				Trakt_Show show = repShows.GetByTraktID(xref.TraktID);
				if (show == null) return;
				if (!show.TvDB_ID.HasValue) return;

				Dictionary<int, int> dictTraktSeasons = null;
				Dictionary<int, Trakt_Episode> dictTraktEpisodes = null;
				Dictionary<int, Trakt_Episode> dictTraktSpecials = null;
				GetDictTraktEpisodesAndSeasons(show, ref dictTraktEpisodes, ref dictTraktSpecials, ref dictTraktSeasons);

				TraktTVPost_ShowEpisodeUnseen postUnseen = new TraktTVPost_ShowEpisodeUnseen();
				postUnseen.episodes = new List<TraktTVSeasonEpisode>();
				postUnseen.SetCredentials();
				postUnseen.imdb_id = "";
				postUnseen.title = show.Title;
				postUnseen.year = show.Year;
				postUnseen.tvdb_id = show.TvDB_ID.Value.ToString();

				int retEpNum = -1;
				int retSeason = -1;

				GetTraktEpisodeNumber(ep, ep.AnimeSeries, show, xref.TraktSeasonNumber, ref retEpNum, ref retSeason, dictTraktEpisodes, dictTraktSpecials, dictTraktSeasons);
				if (retEpNum < 0) return;

				TraktTVSeasonEpisode traktEp = new TraktTVSeasonEpisode();
				traktEp.episode = retEpNum.ToString();
				traktEp.season = retSeason.ToString();
				postUnseen.episodes.Add(traktEp);

				logger.Trace("Marking episode as unwatched on Trakt: {0} - S{1} - EP{2}", show.Title, retSeason, retEpNum);

				string urlUnseen = string.Format(Constants.TraktTvURLs.URLPostShowEpisodeUnseen, Constants.TraktTvURLs.APIKey);
				string json = JSONHelper.Serialize<TraktTVPost_ShowEpisodeUnseen>(postUnseen);

				SendData(urlUnseen, json);

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

				int retEpNum = 0, retSeason = 0;

				CrossRef_AniDB_Trakt xref = series.CrossRefTrakt;
				if (xref == null) return;

				Trakt_ShowRepository repShows = new Trakt_ShowRepository();
				Trakt_Show show = repShows.GetByTraktID(xref.TraktID);
				if (show == null) return;
				if (!show.TvDB_ID.HasValue) return;

				Dictionary<int, int> dictTraktSeasons = null;
				Dictionary<int, Trakt_Episode> dictTraktEpisodes = null;
				Dictionary<int, Trakt_Episode> dictTraktSpecials = null;
				GetDictTraktEpisodesAndSeasons(show, ref dictTraktEpisodes, ref dictTraktSpecials, ref dictTraktSeasons);


				TraktTVPost_ShowEpisodeLibrary postLibrary = new TraktTVPost_ShowEpisodeLibrary();
				postLibrary.episodes = new List<TraktTVSeasonEpisode>();
				postLibrary.SetCredentials();
				postLibrary.imdb_id = "";
				postLibrary.title = show.Title;
				postLibrary.year = show.Year;
				postLibrary.tvdb_id = show.TvDB_ID.Value.ToString();

				TraktTVPost_ShowEpisodeSeen postSeen = new TraktTVPost_ShowEpisodeSeen();
				postSeen.episodes = new List<TraktTVSeasonEpisode>();
				postSeen.SetCredentials();
				postSeen.imdb_id = "";
				postSeen.title = show.Title;
				postSeen.year = show.Year;
				postSeen.tvdb_id = show.TvDB_ID.Value.ToString();

				foreach (AnimeEpisode ep in series.AnimeEpisodes)
				{
					if (ep.VideoLocals.Count > 0)
					{
						retEpNum = -1;
						retSeason = -1;

						GetTraktEpisodeNumber(ep, series, show, xref.TraktSeasonNumber, ref retEpNum, ref retSeason, dictTraktEpisodes, dictTraktSpecials, dictTraktSeasons);
						if (retEpNum < 0) continue;

						TraktTVSeasonEpisode traktEp = new TraktTVSeasonEpisode();
						traktEp.episode = retEpNum.ToString();
						traktEp.season = retSeason.ToString();
						postLibrary.episodes.Add(traktEp);

						AnimeEpisode_User userRecord = null;
						foreach (JMMUser juser in traktUsers)
						{
							userRecord = ep.GetUserRecord(juser.JMMUserID);
							if (userRecord != null) break;
						}

						if (userRecord != null) 
							postSeen.episodes.Add(traktEp);
					}
				}

				if (postLibrary.episodes.Count > 0)
				{
					logger.Info("PostShowEpisodeLibrary: {0}/{1}/{2} eps", show.Title, show.TraktID, postLibrary.episodes.Count);

					string json = JSONHelper.Serialize<TraktTVPost_ShowEpisodeLibrary>(postLibrary);
					string jsonResponse = SendData(url, json);
					logger.Info("PostShowEpisodeLibrary RESPONSE: {0}", jsonResponse);
				}

				if (postSeen.episodes.Count > 0)
				{
					logger.Info("PostShowEpisodeSeen: {0}/{1}/{2} eps", show.Title, show.TraktID, postSeen.episodes.Count);

					string json = JSONHelper.Serialize<TraktTVPost_ShowEpisodeSeen>(postSeen);
					string jsonResponse = SendData(urlSeen, json);
					logger.Info("PostShowEpisodeSeen RESPONSE: {0}", jsonResponse);
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
					CommandRequest_TraktSyncCollectionSeries cmd = new CommandRequest_TraktSyncCollectionSeries(series.AnimeSeriesID, series.Anime.MainTitle);
					cmd.Save();
				}

			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in TraktTVHelper.SyncCollectionToTrakt: " + ex.ToString(), ex);
			}
		}

		private static void GetTraktEpisodeNumber(AnimeEpisode aniepisode, AnimeSeries ser, Trakt_Show show, int season, ref int traktEpNum, ref int traktSeason)
		{
			Dictionary<int, int> dictTraktSeasons = null;
			Dictionary<int, Trakt_Episode> dictTraktEpisodes = null;
			Dictionary<int, Trakt_Episode> dictTraktSpecials = null;
			GetDictTraktEpisodesAndSeasons(show, ref dictTraktEpisodes, ref dictTraktSpecials, ref dictTraktSeasons);

			GetTraktEpisodeNumber(aniepisode, ser, show, season, ref traktEpNum, ref traktSeason, dictTraktEpisodes, dictTraktSpecials, dictTraktSeasons);
		}

		private static void GetTraktEpisodeNumber(AnimeEpisode aniepisode, AnimeSeries ser, Trakt_Show show, int season, ref int traktEpNum, ref int traktSeason,
			Dictionary<int, Trakt_Episode> dictTraktEpisodes, Dictionary<int, Trakt_Episode> dictTraktSpecials, Dictionary<int, int> dictTraktSeasons)
		{
			try
			{
				traktEpNum = -1;
				traktSeason = -1;

				int epNum = aniepisode.AniDB_Episode.EpisodeNumber;

				if (season > 0)
				{
					//episode
					if (aniepisode.EpisodeTypeEnum == AniDBAPI.enEpisodeType.Episode)
					{
						if (dictTraktEpisodes != null && dictTraktSeasons != null)
						{
							if (dictTraktSeasons.ContainsKey(season))
							{
								int absEpisodeNumber = dictTraktSeasons[season] + epNum - 1;
								if (dictTraktEpisodes.ContainsKey(absEpisodeNumber))
								{
									Trakt_Episode tvep = dictTraktEpisodes[absEpisodeNumber];
									traktEpNum = tvep.EpisodeNumber;
									traktSeason = tvep.Season;
								}
							}
						}
					}

					if (aniepisode.EpisodeTypeEnum == AniDBAPI.enEpisodeType.Special)
					{
						traktSeason = 0;
						traktEpNum = epNum;
					}
				}
				else
				{
					traktSeason = 0;
					traktEpNum = epNum;
				}

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

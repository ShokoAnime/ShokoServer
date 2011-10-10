using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using JMMServer.Entities;
using NLog;
using JMMServer.Repositories;
using BinaryNorthwest;


namespace JMMServer.Providers.TraktTV
{
	[DataContract]
	public class TraktTVPost_ShowScrobble
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		// http://trakt.tv/api-docs/show-watching

		[DataMember]
		public string username { get; set; }

		[DataMember]
		public string password { get; set; }

		[DataMember]
		public string imdb_id { get; set; }

		[DataMember]
		public string tvdb_id { get; set; }

		[DataMember]
		public string title { get; set; }

		[DataMember]
		public string year { get; set; }

		[DataMember]
		public string season { get; set; }

		[DataMember]
		public string episode { get; set; }

		[DataMember]
		public string duration { get; set; }

		[DataMember]
		public string progress { get; set; }

		[DataMember]
		public string plugin_version { get; set; }

		[DataMember]
		public string media_center_version { get; set; }

		[DataMember]
		public string media_center_date { get; set; }

		public TraktTVPost_ShowScrobble()
		{
		}

		public void SetCredentials()
		{
			username = ServerSettings.Trakt_Username;
			password = Utils.CalculateSHA1(ServerSettings.Trakt_Password, Encoding.Default);
		}

		public bool Init(AnimeEpisode aniepisode)
		{
			try
			{
				if (string.IsNullOrEmpty(ServerSettings.Trakt_Username) || string.IsNullOrEmpty(ServerSettings.Trakt_Password))
					return false;
				
				username = ServerSettings.Trakt_Username;
				password = Utils.CalculateSHA1(ServerSettings.Trakt_Password, Encoding.Default);

				imdb_id = "";
				AnimeSeries ser = aniepisode.AnimeSeries;
				if (ser == null) return false;

				CrossRef_AniDB_TraktRepository repCrossRef = new CrossRef_AniDB_TraktRepository();
				CrossRef_AniDB_Trakt xref = repCrossRef.GetByAnimeID(ser.AniDB_ID);
				if (xref == null) return false;

				Trakt_ShowRepository repShows = new Trakt_ShowRepository();
				Trakt_Show show = repShows.GetByTraktID(xref.TraktID);
				if (show == null) return false;
				if (!show.TvDB_ID.HasValue) return false;

				tvdb_id = show.TvDB_ID.Value.ToString();
				title = show.Title;
				year = show.Year;

				int retEpNum = 0, retSeason = 0;
				GetTraktEpisodeNumber(aniepisode, show, xref.TraktSeasonNumber, ref retEpNum, ref retSeason);
				if (retEpNum < 0) return false;

				episode = retEpNum.ToString();
				season = retSeason.ToString();

				AniDB_Episode aniep = aniepisode.AniDB_Episode;
				if (aniep != null)
				{
					TimeSpan t = TimeSpan.FromSeconds(aniep.LengthSeconds + 14);
					int toMinutes = int.Parse(Math.Round(t.TotalMinutes).ToString());
					duration = toMinutes.ToString();
				}
				else
					duration = "25";

				progress = "100";

				plugin_version = "0.4";
				media_center_version = "1.2.0.1";
				media_center_date = "Dec 17 2010";

				
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
				return false;
			}

			return true;
		}

		private void GetTraktEpisodeNumber(AnimeEpisode aniepisode, Trakt_Show show, int season, ref int traktEpNum, ref int traktSeason)
		{
			try
			{
				traktEpNum = -1;
				traktSeason = -1;

				AnimeSeries ser = aniepisode.AnimeSeries;
				if (ser == null) return;

				//Dictionary<int, int> dictTraktSeasons = GetDictTraktSeasons(show);
				//Dictionary<int, Trakt_Episode> dictTraktEpisodes = GetDictTraktEpisodes(show);

				Dictionary<int, int> dictTraktSeasons = null;
				Dictionary<int, Trakt_Episode> dictTraktEpisodes = null;
				GetDictTraktEpisodesAndSeasons(show, ref dictTraktEpisodes, ref dictTraktSeasons);

				int epNum = aniepisode.AniDB_Episode.EpisodeNumber;
				
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

				return;
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
				return;
			}
		}

		private void GetDictTraktEpisodesAndSeasons(Trakt_Show show, ref Dictionary<int, Trakt_Episode> dictTraktEpisodes, ref Dictionary<int, int> dictTraktSeasons)
		{
			dictTraktEpisodes = new Dictionary<int, Trakt_Episode>();
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
				int lastSeason = -999;
				foreach (Trakt_Episode ep in eps)
				{
					if (ep.Season == 0) continue;

					dictTraktEpisodes[i] = ep;

					if (ep.Season != lastSeason)
						dictTraktSeasons[ep.Season] = i;

					lastSeason = ep.Season;

					i++;

				}
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
			}
		}
		
	}
}

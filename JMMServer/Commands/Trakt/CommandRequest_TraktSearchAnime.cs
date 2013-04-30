using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using JMMServer.Entities;
using JMMServer.Repositories;
using JMMServer.Providers.TvDB;
using JMMServer.WebCache;
using JMMServer.Providers.TraktTV;
using NHibernate;

namespace JMMServer.Commands
{
	[Serializable]
	public class CommandRequest_TraktSearchAnime : CommandRequestImplementation, ICommandRequest
	{
		public int AnimeID { get; set; }
		public bool ForceRefresh { get; set; }

		public CommandRequestPriority DefaultPriority 
		{
			get { return CommandRequestPriority.Priority9; }
		}

		public string PrettyDescription
		{
			get
			{
				return string.Format("Searching for anime on Trakt.TV: {0}", AnimeID);
			}
		}

		public CommandRequest_TraktSearchAnime()
		{
		}

		public CommandRequest_TraktSearchAnime(int animeID, bool forced)
		{
			this.AnimeID = animeID;
			this.ForceRefresh = forced;
			this.CommandType = (int)CommandRequestType.Trakt_SearchAnime;
			this.Priority = (int)DefaultPriority;

			GenerateCommandID();
		}

		

		public override void ProcessCommand()
		{
			logger.Info("Processing CommandRequest_TraktSearchAnime: {0}", AnimeID);

			try
			{
				using (var session = JMMService.SessionFactory.OpenSession())
				{
					// first check if the user wants to use the web cache
					if (ServerSettings.WebCache_TvDB_Get)
					{
						try
						{
							CrossRef_AniDB_TraktResult crossRef = XMLService.Get_CrossRef_AniDB_Trakt(AnimeID);
							if (crossRef != null)
							{
								TraktTVShow showInfo = TraktTVHelper.GetShowInfo(crossRef.TraktID);
								if (showInfo != null)
								{
									logger.Trace("Found trakt match on web cache for {0} - id = {1}", AnimeID, showInfo.title);
									TraktTVHelper.LinkAniDBTrakt(AnimeID, crossRef.TraktID, crossRef.TraktSeasonNumber, true);
									return;
								}
							}
						}
						catch (Exception ex)
						{
							logger.ErrorException(ex.ToString(), ex);
						}
					}


					// lets try to see locally if we have a tvDB link for this anime
					// Trakt allows the use of TvDB ID's or their own Trakt ID's
					CrossRef_AniDB_TvDBRepository repCrossRefTvDB = new CrossRef_AniDB_TvDBRepository();
					CrossRef_AniDB_TvDB xrefTvDB = repCrossRefTvDB.GetByAnimeID(session, AnimeID);
					if (xrefTvDB != null)
					{
						TraktTVShow showInfo = TraktTVHelper.GetShowInfo(xrefTvDB.TvDBID);
						if (showInfo != null)
						{
							// make sure the season specified by TvDB also exists on Trakt
							Trakt_ShowRepository repShow = new Trakt_ShowRepository();
							Trakt_Show traktShow = repShow.GetByTraktID(session, showInfo.TraktID);
							if (traktShow != null)
							{
								Trakt_SeasonRepository repSeasons = new Trakt_SeasonRepository();
								Trakt_Season traktSeason = repSeasons.GetByShowIDAndSeason(session, traktShow.Trakt_ShowID, xrefTvDB.TvDBSeasonNumber);
								if (traktSeason != null)
								{
									logger.Trace("Found trakt match using TvDBID locally {0} - id = {1}", AnimeID, showInfo.title);
									TraktTVHelper.LinkAniDBTrakt(AnimeID, showInfo.TraktID, traktSeason.Season, true);
									return;
								}
							}
						}
					}

					// if not lets try the tvdb web cache based on the same reasoning
					if (ServerSettings.WebCache_TvDB_Get)
					{
						List<JMMServer.Providers.Azure.CrossRef_AniDB_TvDB> cacheResults = JMMServer.Providers.Azure.AzureWebAPI.Get_CrossRefAniDBTvDB(AnimeID);
						if (cacheResults != null && cacheResults.Count > 0)
						{
							TraktTVShow showInfo = TraktTVHelper.GetShowInfo(cacheResults[0].TvDBID);
							if (showInfo != null)
							{
								// make sure the season specified by TvDB also exists on Trakt
								Trakt_ShowRepository repShow = new Trakt_ShowRepository();
								Trakt_Show traktShow = repShow.GetByTraktID(session, showInfo.TraktID);
								if (traktShow != null)
								{
									Trakt_SeasonRepository repSeasons = new Trakt_SeasonRepository();
									Trakt_Season traktSeason = repSeasons.GetByShowIDAndSeason(session, traktShow.Trakt_ShowID, cacheResults[0].TvDBSeasonNumber);
									if (traktSeason != null)
									{
										logger.Trace("Found trakt match on web cache by using TvDBID {0} - id = {1}", AnimeID, showInfo.title);
										TraktTVHelper.LinkAniDBTrakt(AnimeID, showInfo.TraktID, traktSeason.Season, true);
										return;
									}
								}
							}
						}
					}

					// finally lets try searching Trakt directly
					string searchCriteria = "";
					AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
					AniDB_Anime anime = repAnime.GetByAnimeID(session, AnimeID);
					if (anime == null) return;

					searchCriteria = anime.MainTitle;

					// if not wanting to use web cache, or no match found on the web cache go to TvDB directly
					List<TraktTVShow> results = TraktTVHelper.SearchShow(searchCriteria);
					logger.Trace("Found {0} trakt results for {1} ", results.Count, searchCriteria);
					if (ProcessSearchResults(session, results, searchCriteria)) return;


					if (results.Count == 0)
					{
						foreach (AniDB_Anime_Title title in anime.GetTitles(session))
						{
							if (title.TitleType.ToUpper() != Constants.AnimeTitleType.Official.ToUpper()) continue;

							if (searchCriteria.ToUpper() == title.Title.ToUpper()) continue;

							results = TraktTVHelper.SearchShow(searchCriteria);
							logger.Trace("Found {0} trakt results for search on {1}", results.Count, title.Title);
							if (ProcessSearchResults(session, results, title.Title)) return;
						}
					}
				}

			}
			catch (Exception ex)
			{
				logger.Error("Error processing CommandRequest_TvDBSearchAnime: {0} - {1}", AnimeID, ex.ToString());
				return;
			}
		}

		private bool ProcessSearchResults(ISession session, List<TraktTVShow> results, string searchCriteria)
		{
			if (results.Count == 1)
			{
				// since we are using this result, lets download the info
				logger.Trace("Found 1 trakt results for search on {0} --- Linked to {1} ({2})", searchCriteria, results[0].title, results[0].TraktID);
				TraktTVShow showInfo = TraktTVHelper.GetShowInfo(results[0].TraktID);
				if (showInfo != null)
				{
					TraktTVHelper.LinkAniDBTrakt(session, AnimeID, showInfo.TraktID, 1, false);
					return true;
				}
			}

			return false;
		}
		
		public override void GenerateCommandID()
		{
			this.CommandID = string.Format("CommandRequest_TraktSearchAnime{0}", this.AnimeID);
		}

		public override bool LoadFromDBCommand(CommandRequest cq)
		{
			this.CommandID = cq.CommandID;
			this.CommandRequestID = cq.CommandRequestID;
			this.CommandType = cq.CommandType;
			this.Priority = cq.Priority;
			this.CommandDetails = cq.CommandDetails;
			this.DateTimeUpdated = cq.DateTimeUpdated;

			// read xml to get parameters
			if (this.CommandDetails.Trim().Length > 0)
			{
				XmlDocument docCreator = new XmlDocument();
				docCreator.LoadXml(this.CommandDetails);

				// populate the fields
				this.AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_TraktSearchAnime", "AnimeID"));
				this.ForceRefresh = bool.Parse(TryGetProperty(docCreator, "CommandRequest_TraktSearchAnime", "ForceRefresh"));
			}

			return true;
		}

		public override CommandRequest ToDatabaseObject()
		{
			GenerateCommandID();

			CommandRequest cq = new CommandRequest();
			cq.CommandID = this.CommandID;
			cq.CommandType = this.CommandType;
			cq.Priority = this.Priority;
			cq.CommandDetails = this.ToXML();
			cq.DateTimeUpdated = DateTime.Now;

			return cq;
		}
	}
}

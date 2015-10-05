﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using JMMServer.Entities;
using JMMServer.Repositories;
using JMMServer.Providers.TvDB;
using JMMServer.WebCache;

namespace JMMServer.Commands
{
	[Serializable]
	public class CommandRequest_TvDBSearchAnime : BaseCommandRequest, ICommandRequest
	{
		public int AnimeID { get; set; }
		public bool ForceRefresh { get; set; }

		public CommandRequestPriority DefaultPriority 
		{
			get { return CommandRequestPriority.Priority8; }
		}

		public string PrettyDescription
		{
			get
			{
				return string.Format("Searching for anime on The TvDB: {0}", AnimeID);
			}
		}

		public CommandRequest_TvDBSearchAnime()
		{
		}

		public CommandRequest_TvDBSearchAnime(int animeID, bool forced)
		{
			this.AnimeID = animeID;
			this.ForceRefresh = forced;
			this.CommandType = (int)CommandRequestType.TvDB_SearchAnime;
			this.Priority = (int)DefaultPriority;

			GenerateCommandID();
		}

		public override void ProcessCommand()
		{
			logger.Info("Processing CommandRequest_TvDBSearchAnime: {0}", AnimeID);

			try
			{
				using (var session = JMMService.SessionFactory.OpenSession())
				{
					// first check if the user wants to use the web cache
					if (ServerSettings.WebCache_TvDB_Get)
					{
						try
						{
							List<JMMServer.Providers.Azure.CrossRef_AniDB_TvDB> cacheResults = JMMServer.Providers.Azure.AzureWebAPI.Get_CrossRefAniDBTvDB(AnimeID);
							if (cacheResults != null && cacheResults.Count > 0)
							{
								// check again to see if there are any links, user may have manually added links while
								// this command was in the queue
								CrossRef_AniDB_TvDBV2Repository repCrossRef = new CrossRef_AniDB_TvDBV2Repository();
								List<CrossRef_AniDB_TvDBV2> xrefTemp = repCrossRef.GetByAnimeID(AnimeID);
								if (xrefTemp != null && xrefTemp.Count > 0) return;

								foreach (JMMServer.Providers.Azure.CrossRef_AniDB_TvDB xref in cacheResults)
								{
									TvDB_Series tvser = TvDBHelper.GetSeriesInfoOnline(cacheResults[0].TvDBID);
									if (tvser != null)
									{
										logger.Trace("Found tvdb match on web cache for {0}", AnimeID);
										TvDBHelper.LinkAniDBTvDB(AnimeID, (AniDBAPI.enEpisodeType)cacheResults[0].AniDBStartEpisodeType, cacheResults[0].AniDBStartEpisodeNumber,
											cacheResults[0].TvDBID, cacheResults[0].TvDBSeasonNumber, cacheResults[0].TvDBStartEpisodeNumber, true);
									}
									else
									{
										//if we got a TvDB ID from the web cache, but couldn't find it on TheTvDB.com, it could mean 2 things
										//1. thetvdb.com is offline
										//2. the id is no longer valid
										// if the id is no longer valid we should remove it from the web cache
										/*if (TvDBHelper.ConfirmTvDBOnline())
										{
											
										}*/
									}
								}
								return;
							}
						}
						catch (Exception)
						{
						}
					}

					string searchCriteria = "";
					AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
					AniDB_Anime anime = repAnime.GetByAnimeID(AnimeID);
					if (anime == null) return;

					searchCriteria = anime.MainTitle;

					// if not wanting to use web cache, or no match found on the web cache go to TvDB directly
					List<TVDBSeriesSearchResult> results = JMMService.TvdbHelper.SearchSeries(searchCriteria);
					logger.Trace("Found {0} tvdb results for {1} on TheTvDB", results.Count, searchCriteria);
					if (ProcessSearchResults(results, searchCriteria)) return;


					if (results.Count == 0)
					{
						foreach (AniDB_Anime_Title title in anime.GetTitles())
						{
							if (title.TitleType.ToUpper() != Constants.AnimeTitleType.Official.ToUpper()) continue;

							if (searchCriteria.ToUpper() == title.Title.ToUpper()) continue;

							results = JMMService.TvdbHelper.SearchSeries(title.Title);
							logger.Trace("Found {0} tvdb results for search on {1}", results.Count, title.Title);
							if (ProcessSearchResults(results, title.Title)) return;
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

		private bool ProcessSearchResults(List<TVDBSeriesSearchResult> results, string searchCriteria)
		{
			if (results.Count == 1)
			{
				// since we are using this result, lets download the info
				logger.Trace("Found 1 tvdb results for search on {0} --- Linked to {1} ({2})", searchCriteria, results[0].SeriesName, results[0].SeriesID);
				TvDB_Series tvser = TvDBHelper.GetSeriesInfoOnline(results[0].SeriesID);
				TvDBHelper.LinkAniDBTvDB(AnimeID, AniDBAPI.enEpisodeType.Episode, 1, results[0].SeriesID, 1, 1, true);
				return true;
			}
			else if (results.Count > 1)
			{
				logger.Trace("Found multiple ({0}) tvdb results for search on so checking for english results {1}", results.Count, searchCriteria);
				foreach (TVDBSeriesSearchResult sres in results)
				{
					if (sres.Language.Equals("en", StringComparison.InvariantCultureIgnoreCase))
					{
						// since we are using this result, lets download the info
						logger.Trace("Found english result for search on {0} --- Linked to {1} ({2})", searchCriteria, sres.SeriesName, sres.SeriesID);
						TvDB_Series tvser = TvDBHelper.GetSeriesInfoOnline(results[0].SeriesID);
						TvDBHelper.LinkAniDBTvDB(AnimeID, AniDBAPI.enEpisodeType.Episode, 1, sres.SeriesID, 1, 1, true);
						return true;
					}
				}
				logger.Trace("No english results found, so SKIPPING: {0}", searchCriteria);
			}

			return false;
		}

		public override void GenerateCommandID()
		{
			this.CommandID = string.Format("CommandRequest_TvDBSearchAnime{0}", this.AnimeID);
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
				this.AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_TvDBSearchAnime", "AnimeID"));
				this.ForceRefresh = bool.Parse(TryGetProperty(docCreator, "CommandRequest_TvDBSearchAnime", "ForceRefresh"));
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

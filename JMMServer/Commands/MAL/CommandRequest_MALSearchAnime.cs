using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using JMMServer.Entities;
using JMMServer.Repositories;
using JMMServer.Providers.TvDB;
using JMMServer.WebCache;
using JMMServer.Providers.MyAnimeList;
using AniDBAPI;
using JMMDatabase;
using JMMModels;
using JMMServerModels.DB.Childs;

namespace JMMServer.Commands.MAL
{
	[Serializable]
	public class CommandRequest_MALSearchAnime : BaseCommandRequest, ICommandRequest
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
				return $"Searching for anime on MAL: {AnimeID}";
			}
		}

		public CommandRequest_MALSearchAnime()
		{
		}

		public CommandRequest_MALSearchAnime(int animeID, bool forced)
		{
			this.AnimeID = animeID;
			this.ForceRefresh = forced;
            this.JMMUserId= Store.JmmUserRepo.GetMasterUser().Id;
            this.CommandType = CommandRequestType.MAL_SearchAnime;
			this.Priority =  DefaultPriority;
            this.Id= $"CommandRequest_MALSearchAnime{this.AnimeID}";
		}

		public override void ProcessCommand()
		{
			logger.Info("Processing CommandRequest_MALSearchAnime: {0}", AnimeID);

			try
			{
				// first check if the user wants to use the web cache
				if (ServerSettings.WebCache_MAL_Get)
				{
					try
					{
                        JMMServer.Providers.Azure.CrossRef_AniDB_MAL crossRef = JMMServer.Providers.Azure.AzureWebAPI.Get_CrossRefAniDBMAL(AnimeID);
                        if (crossRef != null)
						{
							logger.Trace("Found MAL match on web cache for {0} - id = {1} ({2}/{3})", AnimeID, crossRef.MALID, crossRef.StartEpisodeType, crossRef.StartEpisodeNumber);
							MALHelper.LinkAniDBMAL(JMMUserId, AnimeID, crossRef.MALID, crossRef.MALTitle, crossRef.StartEpisodeType, crossRef.StartEpisodeNumber, true);

							return;
						}
					}
					catch (Exception)
					{
						
					}
				}
			    AnimeSerie ser = Store.AnimeSerieRepo.AnimeSerieFromAniDBAnime(AnimeID.ToString());
			    if (ser == null)
			        return;

                string searchCriteria = ser.AniDB_Anime.MainTitle;

				// if not wanting to use web cache, or no match found on the web cache go to TvDB directly
				anime malResults = MALHelper.SearchAnimesByTitle(searchCriteria);

				if (malResults.entry.Length == 1)
				{
					logger.Trace("Using MAL search result for search on {0} : {1} ({2})", searchCriteria, malResults.entry[0].id, malResults.entry[0].title);
					MALHelper.LinkAniDBMAL(AnimeID, malResults.entry[0].id, malResults.entry[0].title, (int)enEpisodeType.Episode, 1, false);
				}
				else if (malResults.entry.Length == 0)
					logger.Trace("ZERO MAL search result results for: {0}", searchCriteria);
				else
				{
					// if the title's match exactly and they have the same amount of episodes, we will use it
					foreach (animeEntry res in malResults.entry)
					{
						if (res.title.Equals(searchCriteria, StringComparison.InvariantCultureIgnoreCase) && res.episodes == ser.AniDB_Anime.EpisodeCountNormal)
						{
							logger.Trace("Using MAL search result for search on {0} : {1} ({2})", searchCriteria, res.id, res.title);
							MALHelper.LinkAniDBMAL(AnimeID, res.id, res.title, (int)enEpisodeType.Episode, 1, false);
						}
					}
					logger.Trace("Too many MAL search result results for, skipping: {0}", searchCriteria);
				}
			}
			catch (Exception ex)
			{
				logger.Error("Error processing CommandRequest_MALSearchAnime: {0} - {1}", AnimeID, ex.ToString());
				return;
			}
		}
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Repositories;
using JMMServer.Entities;
using JMMServer.WebCache;
using System.Xml;
using JMMDatabase;
using JMMModels;
using JMMModels.Childs;
using JMMServer.Providers.Azure;
using JMMServerModels.DB.Childs;

namespace JMMServer.Commands.WebCache
{
	public class CommandRequest_WebCacheSendXRefAniDBMAL : BaseCommandRequest, ICommandRequest
	{
        public int AnimeID { get; set; }
        public int MalID { get; set; }

		public CommandRequestPriority DefaultPriority
		{
			get { return CommandRequestPriority.Priority9; }
		}

		public string PrettyDescription
		{
			get
			{
				return $"Sending cross ref for Anidb to MAL from web cache AnimeID: {AnimeID} MalID {MalID}";
			}
		}

		public CommandRequest_WebCacheSendXRefAniDBMAL()
		{
		}

		public CommandRequest_WebCacheSendXRefAniDBMAL(int animeId, int malId)
		{
		    this.AnimeID = animeId;
		    this.MalID = malId;        
			this.CommandType = CommandRequestType.WebCache_SendXRefAniDBMAL;
			this.Priority = DefaultPriority;
            this.JMMUserId = Store.JmmUserRepo.GetMasterUser().Id;
            this.Id= $"CommandRequest_WebCacheSendXRefAniDBMAL{AnimeID}_{MalID}";

		}

		public override void ProcessCommand()
		{
			
			try
			{
			    AnimeSerie ser = Store.AnimeSerieRepo.AnimeSerieFromAniDBAnime(AnimeID.ToString());
			    if (ser == null)
			        return;
			    AniDB_Anime_MAL mal = ser.AniDB_Anime.MALs.FirstOrDefault(a => a.MalId == MalID.ToString());
			    if (mal == null)
			        return;
                AzureWebAPI.Send_CrossRefAniDBMAL(JMMUserId, AnimeID, mal);
			}
			catch (Exception ex)
			{
				logger.ErrorException("Error processing CommandRequest_WebCacheSendXRefAniDBMAL: {0}" + ex.ToString(), ex);
				return;
			}
		}
	}
}

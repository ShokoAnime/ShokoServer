using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using JMMDatabase;
using JMMServer.Entities;
using JMMServer.Repositories;
using JMMServer.Providers.Azure;
using JMMServerModels.DB.Childs;

namespace JMMServer.Commands
{
	[Serializable]
	public class CommandRequest_GetAnimeHTTP : BaseCommandRequest, ICommandRequest
	{
		public int AnimeID { get; set; }
		public bool ForceRefresh { get; set; }
		public bool DownloadRelations { get; set; }

		public CommandRequestPriority DefaultPriority 
		{
			get { return CommandRequestPriority.Priority2; }
		}

		public string PrettyDescription
		{
			get
			{
				return string.Format("Getting anime info from HTTP API: {0}", AnimeID);
			}
		}

		public CommandRequest_GetAnimeHTTP()
		{
		}

		public CommandRequest_GetAnimeHTTP(int animeid, bool forced, bool downloadRelations)
		{
			this.AnimeID = animeid;
			this.DownloadRelations = downloadRelations;
			this.ForceRefresh = forced;
			this.CommandType = CommandRequestType.AniDB_GetAnimeHTTP;
			this.Priority = DefaultPriority;
            this.JMMUserId = Store.JmmUserRepo.GetMasterUser().Id;
            this.Id= $"CommandRequest_GetAnimeHTTP_{AnimeID}";
		}

		public override void ProcessCommand()
		{
			logger.Info("Processing CommandRequest_GetAnimeHTTP: {0}", AnimeID);

			try
			{
				//AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
				//AniDB_Anime anime = 
                    
                JMMService.AnidbProcessor.GetAnimeInfoHTTP(AnimeID, ForceRefresh, DownloadRelations);
				
				// NOTE - related anime are downloaded when the relations are created
				
				// download group status info for this anime
				// the group status will also help us determine missing episodes for a series


				// download reviews
				if (ServerSettings.AniDB_DownloadReviews)
				{
					CommandRequest_GetReviews cmd = new CommandRequest_GetReviews(AnimeID, ForceRefresh);
					cmd.Save();
				}

				// Request an image download

			}
			catch (Exception ex)
			{
				logger.Error("Error processing CommandRequest_GetAnimeHTTP: {0} - {1}", AnimeID, ex.ToString());
				return;
			}
		}
	}
}

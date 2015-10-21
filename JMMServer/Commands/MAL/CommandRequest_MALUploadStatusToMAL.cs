using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.WebCache;
using JMMServer.Providers.MyAnimeList;
using AniDBAPI;
using System.Xml;
using JMMDatabase;
using JMMDatabase.Extensions;
using JMMModels.Childs;
using JMMServer.Entities;
using JMMServer.Repositories;
using JMMServerModels.DB.Childs;
using AniDB_Anime = JMMServer.Entities.AniDB_Anime;

namespace JMMServer.Commands.MAL
{
	[Serializable]
	public class CommandRequest_MALUploadStatusToMAL : BaseCommandRequest, ICommandRequest
	{
		public CommandRequestPriority DefaultPriority 
		{
			get { return CommandRequestPriority.Priority9; }
		}

		public string PrettyDescription
		{
			get
			{
				return string.Format("Uploading watched states to MAL");
			}
		}


		public CommandRequest_MALUploadStatusToMAL(string userid)
		{
			this.CommandType = CommandRequestType.MAL_UploadWatchedStates;
			this.Priority = DefaultPriority;
		    this.JMMUserId = userid;
            this.Id= "CommandRequest_MALUploadStatusToMAL";
		}

		public override void ProcessCommand()
		{
			logger.Info("Processing CommandRequest_MALUploadStatusToMAL");

			try
			{
                JMMModels.JMMUser user = Store.JmmUserRepo.Find(JMMUserId);
			    if (user == null)
			        return;
                user = user.GetUserWithAuth(AuthorizationProvider.AniDB);
			    if (user == null)
			        return;
                UserNameAuthorization auth = user.GetMALAuthorization();

                if (string.IsNullOrEmpty(auth.UserName) || string.IsNullOrEmpty(auth.Password))
					return;

				// find the latest eps to update
				AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
				List<AniDB_Anime> animes = repAnime.GetAll();

				foreach (AniDB_Anime anime in animes)
				{
					CommandRequest_MALUpdatedWatchedStatus cmd = new CommandRequest_MALUpdatedWatchedStatus(user.Id, anime.AnimeID);
					cmd.Save();
				}
			}
			catch (Exception ex)
			{
				logger.Error("Error processing CommandRequest_MALUploadStatusToMAL: {0}", ex.ToString());
				return;
			}
		}
	}
}

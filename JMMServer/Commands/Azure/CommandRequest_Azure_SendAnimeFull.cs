using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Repositories;
using JMMServer.Entities;
using JMMServer.Providers.Azure;
using System.Xml;
using JMMDatabase;
using JMMDatabase.Extensions;
using JMMModels.Childs;
using JMMServerModels.DB.Childs;
using AniDB_Anime = JMMServer.Entities.AniDB_Anime;

namespace JMMServer.Commands.Azure
{
	public class CommandRequest_Azure_SendAnimeFull : BaseCommandRequest, ICommandRequest
	{
		public int AnimeID { get; set; }

		public CommandRequestPriority DefaultPriority
		{
			get { return CommandRequestPriority.Priority9; }
		}

		public string PrettyDescription
		{
			get
			{
				return string.Format("Sending anime full to azure: {0}", AnimeID);
			}
		}

		public CommandRequest_Azure_SendAnimeFull()
		{
		}

		public CommandRequest_Azure_SendAnimeFull(int animeID)
		{
			this.AnimeID = animeID;
			this.CommandType = CommandRequestType.Azure_SendAnimeFull;
			this.Priority = DefaultPriority;
            this.JMMUserId= Store.JmmUserRepo.GetMasterUser().Id;
		    this.Id = $"CommandRequest_Azure_SendAnimeFull_{this.AnimeID}";

		}

		public override void ProcessCommand()
		{
			
			try
			{
                JMMModels.JMMUser user = Store.JmmUserRepo.Find(JMMUserId).GetUserWithAuth(AuthorizationProvider.AniDB);
			    if (user == null)
			        return;
                AniDBAuthorization auth = user.GetAniDBAuthorization();
                bool process = (auth.UserName.Equals("jonbaby", StringComparison.InvariantCultureIgnoreCase) ||
                    auth.UserName.Equals("jmediamanager", StringComparison.InvariantCultureIgnoreCase) ||
                    auth.UserName.Equals("jmmtesting", StringComparison.InvariantCultureIgnoreCase));

				if (!process) return;

				AniDB_AnimeRepository rep = new AniDB_AnimeRepository();
				AniDB_Anime anime = rep.GetByAnimeID(AnimeID);
				if (anime == null) return;

				if (anime.AllTags.ToUpper().Contains("18 RESTRICTED")) return;

				AzureWebAPI.Send_AnimeFull(anime);
			}
			catch (Exception ex)
			{
				logger.Error("Error processing CommandRequest_Azure_SendAnimeFull: {0} - {1}", AnimeID, ex.ToString());
				return;
			}
		}

	}
}

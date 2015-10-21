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

namespace JMMServer.Commands.Azure
{
	public class CommandRequest_Azure_SendAnimeTitle : BaseCommandRequest, ICommandRequest
	{
		public int AnimeID { get; set; }
		public string MainTitle { get; set; }
		public string Titles { get; set; }

		public CommandRequestPriority DefaultPriority
		{
			get { return CommandRequestPriority.Priority11; }
		}

		public string PrettyDescription
		{
			get
			{
				return $"Sending anime title to azure: {AnimeID}";
			}
		}

		public CommandRequest_Azure_SendAnimeTitle()
		{
		}

		public CommandRequest_Azure_SendAnimeTitle(int animeID, string main, string titles)
		{
			this.AnimeID = animeID;
			this.MainTitle = main;
			this.Titles = titles;
			this.CommandType = CommandRequestType.Azure_SendAnimeTitle;
			this.Priority = DefaultPriority;
            this.JMMUserId = Store.JmmUserRepo.GetMasterUser().Id;
            this.Id= $"CommandRequest_Azure_SendAnimeTitle_{this.AnimeID}";
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
                    auth.UserName.Equals("jmediamanager", StringComparison.InvariantCultureIgnoreCase));

				if (!process) return;

				AnimeIDTitle thisTitle = new AnimeIDTitle();
				thisTitle.AnimeIDTitleId = 0;
				thisTitle.MainTitle = MainTitle;
				thisTitle.AnimeID = AnimeID;
				thisTitle.Titles = Titles;

				AzureWebAPI.Send_AnimeTitle(thisTitle);
			}
			catch (Exception ex)
			{
				logger.Error("Error processing CommandRequest_Azure_SendAnimeTitle: {0} - {1}", AnimeID, ex.ToString());
				return;
			}
		}
	}
}

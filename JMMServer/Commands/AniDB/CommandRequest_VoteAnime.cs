using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Text;
using JMMServer.Repositories;
using JMMServer.Entities;
using System.Xml;
using JMMServer.Commands.MAL;
using System.Globalization;
using JMMDatabase;
using JMMDatabase.Extensions;
using JMMModels.Childs;
using JMMServerModels.DB.Childs;

namespace JMMServer.Commands
{
	[Serializable]
	public class CommandRequest_VoteAnime : BaseCommandRequest, ICommandRequest
	{
		public int AnimeID { get; set; }
		public int VoteType { get; set; }
		public decimal VoteValue { get; set; }

		public CommandRequestPriority DefaultPriority
		{
			get { return CommandRequestPriority.Priority8; }
		}

		public string PrettyDescription
		{
			get
			{
				return $"Voting: {AnimeID} - {VoteValue}";
			}
		}

		public CommandRequest_VoteAnime()
		{
		}

		public CommandRequest_VoteAnime(string userid, int animeID, int voteType, decimal voteValue)
		{
			this.AnimeID = animeID;
			this.VoteType = voteType;
			this.VoteValue = voteValue;
		    this.JMMUserId = userid;
            this.CommandType = CommandRequestType.AniDB_VoteAnime;
			this.Priority = DefaultPriority;
            this.Id= $"CommandRequest_Vote_{AnimeID}_{(int) VoteType}_{VoteValue}";
		}

		public override void ProcessCommand()
		{
			logger.Info("Processing CommandRequest_Vote: {0}", Id);

			
			try
			{
                JMMModels.JMMUser user = Store.JmmUserRepo.Find(JMMUserId).GetUserWithAuth(AuthorizationProvider.AniDB);
                if (user == null)
                    return;


                JMMService.AnidbProcessor.VoteAnime(user.Id, AnimeID, VoteValue, (AniDBAPI.enAniDBVoteType)VoteType);

				if (!string.IsNullOrEmpty(ServerSettings.MAL_Username) && !string.IsNullOrEmpty(ServerSettings.MAL_Password))
				{
					CommandRequest_MALUpdatedWatchedStatus cmdMAL = new CommandRequest_MALUpdatedWatchedStatus(AnimeID);
					cmdMAL.Save();
				}
			}
			catch (Exception ex)
			{
				logger.Error("Error processing CommandRequest_Vote: {0} - {1}", Id, ex.ToString());
				return;
			}
		}
	}
}

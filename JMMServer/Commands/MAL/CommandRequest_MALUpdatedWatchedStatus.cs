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
using JMMModels;
using JMMModels.Childs;
using JMMServer.Entities;
using JMMServer.Repositories;
using JMMServerModels.DB.Childs;

namespace JMMServer.Commands.MAL
{
	[Serializable]
	public class CommandRequest_MALUpdatedWatchedStatus : BaseCommandRequest, ICommandRequest
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
				return $"Updating status on MAL: {AnimeID}";
			}
		}

		public CommandRequest_MALUpdatedWatchedStatus()
		{
		}

		public CommandRequest_MALUpdatedWatchedStatus(string userid,int animeID)
		{
			AnimeID = animeID;
		    JMMUserId = userid;
			CommandType = CommandRequestType.MAL_UpdateStatus;
			Priority = DefaultPriority;
            Id = $"CommandRequest_MALUpdatedWatchedStatus_{AnimeID}";
		}

		public override void ProcessCommand()
		{
			logger.Info("Processing CommandRequest_MALUpdatedWatchedStatus: {0}", AnimeID);

			try
			{
			    JMMModels.JMMUser user = Store.JmmUserRepo.Find(JMMUserId);
			    if (user == null)
			        return;
			    user = user.GetUserWithAuth(AuthorizationProvider.MAL);

			    AnimeSerie ser = Store.AnimeSerieRepo.AnimeSerieFromAniDBAnime(AnimeID.ToString());
			    if (ser.AniDB_Anime.MALs == null || ser.AniDB_Anime.MALs.Count == 0)
			        return;

				MALHelper.UpdateMALSeries(user.Id, ser);

			}
			catch (Exception ex)
			{
				logger.Error("Error processing CommandRequest_MALUpdatedWatchedStatus: {0} - {1}", AnimeID, ex.ToString());
				return;
			}
		}

	}
}

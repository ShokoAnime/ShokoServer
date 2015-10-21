using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using System.Xml;
using JMMDatabase;
using JMMDatabase.Extensions;
using JMMModels.Childs;
using JMMServer.Repositories;
using JMMServer.Providers.TraktTV;
using JMMServerModels.DB.Childs;
using Newtonsoft.Json;
using AnimeEpisode = JMMServer.Entities.AnimeEpisode;

namespace JMMServer.Commands
{
	[Serializable]
	public class CommandRequest_TraktHistoryEpisode : BaseCommandRequest, ICommandRequest
	{
		public string AnimeEpisodeID { get; set; }

        public TraktSyncAction Action { get; set; }

        [JsonIgnore]
	    public CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority9;

	    public string PrettyDescription
	    {
	        get
	        {
                JMMModels.JMMUser user = Store.JmmUserRepo.Find(JMMUserId);
                if (user != null)
                    return $"Add episode to history of {user.UserName} on Trakt: {AnimeEpisodeID}";
                return string.Empty;
            }
        }
	    public CommandRequest_TraktHistoryEpisode()
		{
		}

        public CommandRequest_TraktHistoryEpisode(string userid, string animeEpisodeID, TraktSyncAction action)
		{
			this.AnimeEpisodeID = animeEpisodeID;
            this.Action = action;
            JMMModels.JMMUser user = Store.JmmUserRepo.Find(userid).GetUserWithAuth(AuthorizationProvider.Trakt);
            if (user != null)
                userid = user.Id;
            this.JMMUserId = userid;
            this.CommandType = CommandRequestType.Trakt_EpisodeHistory;
			this.Priority = DefaultPriority;
            this.Id= $"CommandRequest_TraktHistoryEpisode{JMMUserId}-{AnimeEpisodeID}-{Action}";
		}

		public override void ProcessCommand()
		{
            logger.Info("Processing CommandRequest_TraktHistoryEpisode: {0}-{1}", AnimeEpisodeID, Action);

			try
			{
                JMMModels.JMMUser user = Store.JmmUserRepo.Find(JMMUserId);
                TraktAuthorization trakt = user.GetTraktAuthorization();

                if (!ServerSettings.Trakt_IsEnabled || string.IsNullOrEmpty(trakt.Trakt_AuthToken)) return;

                JMMModels.AnimeSerie serie = Store.AnimeSerieRepo.AnimeSerieFromAnimeEpisode(AnimeEpisodeID);
                if (serie == null)
                    return;
                JMMModels.Childs.AnimeEpisode ep = serie.Episodes.FirstOrDefault(a => a.Id == AnimeEpisodeID);
                if (ep != null)
                {
                    TraktSyncType syncType = TraktSyncType.HistoryAdd;
                    if (Action == TraktSyncAction.Remove) syncType = TraktSyncType.HistoryRemove;
                    TraktTVHelper.SyncEpisodeToTrakt(user.Id, ep, syncType);
				}
			}
			catch (Exception ex)
			{
                logger.Error("Error processing CommandRequest_TraktHistoryEpisode: {0} - {1}", AnimeEpisodeID, ex.ToString());
				return;
			}
		}
	}
}

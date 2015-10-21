using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using JMMDatabase;
using JMMDatabase.Extensions;
using JMMModels.Childs;
using JMMServer.Providers.TraktTV;
using JMMServer.Repositories;
using JMMServerModels.DB.Childs;
using AnimeEpisode = JMMServer.Entities.AnimeEpisode;

namespace JMMServer.Commands
{
    [Serializable]
    public class CommandRequest_TraktCollectionEpisode : BaseCommandRequest, ICommandRequest
    {
        public string AnimeEpisodeID { get; set; }
        public TraktSyncAction Action { get; set; }

        public TraktSyncAction ActionEnum
        {
            get
            {
                return (TraktSyncAction)Action;
            }
        }

		public CommandRequestPriority DefaultPriority
		{
			get { return CommandRequestPriority.Priority9; }
		}

		public string PrettyDescription
		{
			get
			{
			    JMMModels.JMMUser user = Store.JmmUserRepo.Find(JMMUserId);
			    if (user != null)
			        return $"Sync episode to collection of {user.UserName} on Trakt: {AnimeEpisodeID} - {Action}";
			    return string.Empty;
			}
		}

		public CommandRequest_TraktCollectionEpisode()
		{
		}

        public CommandRequest_TraktCollectionEpisode(string userid, string animeEpisodeID, TraktSyncAction action)
		{
			this.AnimeEpisodeID = animeEpisodeID;
            this.Action = action;
            JMMModels.JMMUser user = Store.JmmUserRepo.Find(userid).GetUserWithAuth(AuthorizationProvider.Trakt);
            if (user != null)
                userid = user.Id;
            this.JMMUserId = userid;
            this.CommandType = CommandRequestType.Trakt_EpisodeCollection;
			this.Priority = DefaultPriority;
            this.Id= $"CommandRequest_TraktCollectionEpisode{userid}-{AnimeEpisodeID}-{Action}";
		}

		public override void ProcessCommand()
		{
            logger.Info("Processing CommandRequest_TraktCollectionEpisode: {0}-{1}", AnimeEpisodeID, Action);

			try
			{
			    JMMModels.JMMUser user = Store.JmmUserRepo.Find(JMMUserId);
			    TraktAuthorization trakt = user.GetTraktAuthorization();

                logger.Info("CommandRequest_TraktCollectionEpisode - DEBUG01");
                if (!ServerSettings.Trakt_IsEnabled || string.IsNullOrEmpty(trakt.Trakt_AuthToken)) return;
                logger.Info("CommandRequest_TraktCollectionEpisode - DEBUG02");
			    JMMModels.AnimeSerie serie = Store.AnimeSerieRepo.AnimeSerieFromAnimeEpisode(AnimeEpisodeID);
			    if (serie == null)
			        return;
			    JMMModels.Childs.AnimeEpisode ep = serie.Episodes.FirstOrDefault(a => a.Id == AnimeEpisodeID);
				if (ep != null)
				{
                    logger.Info("CommandRequest_TraktCollectionEpisode - DEBUG03");
                    TraktSyncType syncType = TraktSyncType.CollectionAdd;
                    if (ActionEnum == TraktSyncAction.Remove) syncType = TraktSyncType.CollectionRemove;
                    TraktTVHelper.SyncEpisodeToTrakt(user.Id, ep, syncType);
				}
			}
			catch (Exception ex)
			{
                logger.Error("Error processing CommandRequest_TraktCollectionEpisode: {0} - {1} - {2}", AnimeEpisodeID, Action, ex.ToString());
				return;
			}
		}

    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using System.Xml;
using JMMServer.Repositories;
using JMMServer.Providers.TraktTV;
using System.Collections.Specialized;
using System.Threading;
using System.Globalization;
using System.Configuration;

namespace JMMServer.Commands
{
	[Serializable]
	public class CommandRequest_TraktHistoryEpisode : CommandRequestImplementation, ICommandRequest
	{
		public int AnimeEpisodeID { get; set; }
        public int Action { get; set; }

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
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                return string.Format(JMMServer.Properties.Resources.Command_TraktAddHistory, AnimeEpisodeID);
			}
		}

		public CommandRequest_TraktHistoryEpisode()
		{
		}

        public CommandRequest_TraktHistoryEpisode(int animeEpisodeID, TraktSyncAction action)
		{
			this.AnimeEpisodeID = animeEpisodeID;
            this.Action = (int)action;
			this.CommandType = (int)CommandRequestType.Trakt_EpisodeHistory;
			this.Priority = (int)DefaultPriority;

			GenerateCommandID();
		}

		public override void ProcessCommand()
		{
            logger.Info("Processing CommandRequest_TraktHistoryEpisode: {0}-{1}", AnimeEpisodeID, Action);

			try
			{
                if (!ServerSettings.Trakt_IsEnabled || string.IsNullOrEmpty(ServerSettings.Trakt_AuthToken)) return;

				AnimeEpisodeRepository repEpisodes = new AnimeEpisodeRepository();
				AnimeEpisode ep = repEpisodes.GetByID(AnimeEpisodeID);
				if (ep != null)
				{
                    TraktSyncType syncType = TraktSyncType.HistoryAdd;
                    if (ActionEnum == TraktSyncAction.Remove) syncType = TraktSyncType.HistoryRemove;
                    TraktTVHelper.SyncEpisodeToTrakt(ep, syncType);
				}
			}
			catch (Exception ex)
			{
                logger.Error("Error processing CommandRequest_TraktHistoryEpisode: {0} - {1}", AnimeEpisodeID, ex.ToString());
				return;
			}
		}

		/// <summary>
		/// This should generate a unique key for a command
		/// It will be used to check whether the command has already been queued before adding it
		/// </summary>
		public override void GenerateCommandID()
		{
            this.CommandID = string.Format("CommandRequest_TraktHistoryEpisode{0}-{1}", AnimeEpisodeID, Action);
		}

		public override bool LoadFromDBCommand(CommandRequest cq)
		{
			this.CommandID = cq.CommandID;
			this.CommandRequestID = cq.CommandRequestID;
			this.CommandType = cq.CommandType;
			this.Priority = cq.Priority;
			this.CommandDetails = cq.CommandDetails;
			this.DateTimeUpdated = cq.DateTimeUpdated;

			// read xml to get parameters
			if (this.CommandDetails.Trim().Length > 0)
			{
				XmlDocument docCreator = new XmlDocument();
				docCreator.LoadXml(this.CommandDetails);

				// populate the fields
                this.AnimeEpisodeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_TraktHistoryEpisode", "AnimeEpisodeID"));
                this.Action = int.Parse(TryGetProperty(docCreator, "CommandRequest_TraktHistoryEpisode", "Action"));
			}

			return true;
		}

		public override CommandRequest ToDatabaseObject()
		{
			GenerateCommandID();

			CommandRequest cq = new CommandRequest();
			cq.CommandID = this.CommandID;
			cq.CommandType = this.CommandType;
			cq.Priority = this.Priority;
			cq.CommandDetails = this.ToXML();
			cq.DateTimeUpdated = DateTime.Now;

			return cq;
		}
	}
}

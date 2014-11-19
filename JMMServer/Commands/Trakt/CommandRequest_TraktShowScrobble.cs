using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using System.Xml;
using JMMServer.Repositories;
using JMMServer.Providers.TraktTV;

namespace JMMServer.Commands
{
	[Serializable]
	public class CommandRequest_TraktShowScrobble : CommandRequestImplementation, ICommandRequest
	{
		public int AnimeEpisodeID { get; set; }

		public CommandRequestPriority DefaultPriority
		{
			get { return CommandRequestPriority.Priority9; }
		}

		public string PrettyDescription
		{
			get
			{
				return string.Format("Scrobbling show on Trakt: {0}", AnimeEpisodeID);
			}
		}

		public CommandRequest_TraktShowScrobble()
		{
		}

		public CommandRequest_TraktShowScrobble(int animeEpisodeID)
		{
			this.AnimeEpisodeID = animeEpisodeID;
			this.CommandType = (int)CommandRequestType.Trakt_ShowScrobble;
			this.Priority = (int)DefaultPriority;

			GenerateCommandID();
		}

		public override void ProcessCommand()
		{
			logger.Info("Processing CommandRequest_TraktShowScrobble: {0}", AnimeEpisodeID);

			try
			{
                if (!ServerSettings.WebCache_Trakt_Send || string.IsNullOrEmpty(ServerSettings.Trakt_Username)) return;

				AnimeEpisodeRepository repEpisodes = new AnimeEpisodeRepository();
				AnimeEpisode ep = repEpisodes.GetByID(AnimeEpisodeID);
				if (ep != null)
				{
					// before scrobbling try to ensure the episode is in the users collection by sync'ing the series
					//TraktTVHelper.SyncCollectionToTrakt_Series(ep.AnimeSeries);

					TraktTVHelper.MarkEpisodeWatched(ep);
				}
			}
			catch (Exception ex)
			{
				logger.Error("Error processing CommandRequest_TraktShowScrobble: {0} - {1}", AnimeEpisodeID, ex.ToString());
				return;
			}
		}

		/// <summary>
		/// This should generate a unique key for a command
		/// It will be used to check whether the command has already been queued before adding it
		/// </summary>
		public override void GenerateCommandID()
		{
			this.CommandID = string.Format("CommandRequest_TraktShowScrobble{0}", AnimeEpisodeID);
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
				this.AnimeEpisodeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_TraktShowScrobble", "AnimeEpisodeID"));
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

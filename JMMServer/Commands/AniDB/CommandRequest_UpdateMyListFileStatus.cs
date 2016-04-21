using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using System.Xml;
using JMMServer.Repositories;
using System.Collections.Specialized;
using System.Threading;
using System.Globalization;
using System.Configuration;

namespace JMMServer.Commands
{
	[Serializable]
	public class CommandRequest_UpdateMyListFileStatus : CommandRequestImplementation, ICommandRequest
	{
		public string FullFileName { get; set; }
		public string Hash { get; set; }
		public bool Watched { get; set; }
		public bool UpdateSeriesStats { get; set; }
		public int WatchedDateAsSecs { get; set; }

		public CommandRequestPriority DefaultPriority
		{
			get { return CommandRequestPriority.Priority8; }
		}

		public string PrettyDescription
		{
			get
			{
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                return string.Format(JMMServer.Properties.Resources.Command_UpdateMyListInfo, FullFileName);
			}
		}

		public CommandRequest_UpdateMyListFileStatus()
		{
		}

		public CommandRequest_UpdateMyListFileStatus(string hash, bool watched, bool updateSeriesStats, int watchedDateSecs)
		{
			this.Hash = hash;
			this.Watched = watched;
			this.CommandType = (int)CommandRequestType.AniDB_UpdateWatchedUDP;
			this.Priority = (int)DefaultPriority;
			this.UpdateSeriesStats = updateSeriesStats;
			this.WatchedDateAsSecs = watchedDateSecs;

			GenerateCommandID();
		}

		public override void ProcessCommand()
		{
			logger.Info("Processing CommandRequest_UpdateMyListFileStatus: {0}", Hash);

			
			try
			{
				VideoLocalRepository repVids = new VideoLocalRepository();
				AnimeEpisodeRepository repEpisodes = new AnimeEpisodeRepository();

				// NOTE - we might return more than one VideoLocal record here, if there are duplicates by hash
				VideoLocal vid = repVids.GetByHash(this.Hash);
				if (vid != null)
				{
					bool isManualLink = false;
					List<CrossRef_File_Episode> xrefs = vid.EpisodeCrossRefs;
					if (xrefs.Count > 0)
						isManualLink = xrefs[0].CrossRefSource != (int)CrossRefSource.AniDB;

					if (isManualLink)
					{
						JMMService.AnidbProcessor.UpdateMyListFileStatus(xrefs[0].AnimeID, xrefs[0].Episode.EpisodeNumber, this.Watched);
						logger.Info("Updating file list status (GENERIC): {0} - {1}", vid.ToString(), this.Watched);
					}
					else
					{
						if (WatchedDateAsSecs > 0)
						{
							DateTime? watchedDate = Utils.GetAniDBDateAsDate(WatchedDateAsSecs);
							JMMService.AnidbProcessor.UpdateMyListFileStatus(vid, this.Watched, watchedDate);
						}
						else
							JMMService.AnidbProcessor.UpdateMyListFileStatus(vid, this.Watched, null);
						logger.Info("Updating file list status: {0} - {1}", vid.ToString(), this.Watched);
					}

					if (UpdateSeriesStats)
					{
						// update watched stats
						List<AnimeEpisode> eps = repEpisodes.GetByHash(vid.ED2KHash);
						if (eps.Count > 0)
						{
							// all the eps should belong to the same anime
							eps[0].GetAnimeSeries().QueueUpdateStats();
							//eps[0].AnimeSeries.TopLevelAnimeGroup.UpdateStatsFromTopLevel(true, true, false);
						}
					}
				}

				
			}
			catch (Exception ex)
			{
				logger.Error("Error processing CommandRequest_UpdateMyListFileStatus: {0} - {1}", Hash, ex.ToString());
				return;
			}
		}

		/// <summary>
		/// This should generate a unique key for a command
		/// It will be used to check whether the command has already been queued before adding it
		/// </summary>
		public override void GenerateCommandID()
		{
			this.CommandID = string.Format("CommandRequest_UpdateMyListFileStatus_{0}_{1}", Hash, Guid.NewGuid().ToString());
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
				this.Hash = TryGetProperty(docCreator, "CommandRequest_UpdateMyListFileStatus", "Hash");
				this.Watched = bool.Parse(TryGetProperty(docCreator, "CommandRequest_UpdateMyListFileStatus", "Watched"));

				string sUpStats = TryGetProperty(docCreator, "CommandRequest_UpdateMyListFileStatus", "UpdateSeriesStats");
				bool upStats = true;
				if (bool.TryParse(sUpStats, out upStats))
					UpdateSeriesStats = upStats;

				int dateSecs = 0;
				if (int.TryParse(TryGetProperty(docCreator, "CommandRequest_UpdateMyListFileStatus", "WatchedDateAsSecs"), out dateSecs))
					WatchedDateAsSecs = dateSecs;
			}

			if (this.Hash.Trim().Length > 0)
				return true;
			else
				return false;
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

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
	public class CommandRequest_TraktSyncCollectionSeries : CommandRequestImplementation, ICommandRequest
	{
		public int AnimeSeriesID { get; set; }
		public string SeriesName { get; set; }

		public CommandRequestPriority DefaultPriority
		{
			get { return CommandRequestPriority.Priority9; }
		}

		public string PrettyDescription
		{
			get
			{
				return string.Format("Sync'ing Trakt Collection for series: {0}", SeriesName);
			}
		}

		public CommandRequest_TraktSyncCollectionSeries()
		{
		}

		public CommandRequest_TraktSyncCollectionSeries(int animeSeriesID, string seriesName)
		{
			this.AnimeSeriesID = animeSeriesID;
			this.SeriesName = seriesName;
			this.CommandType = (int)CommandRequestType.Trakt_SyncCollectionSeries;
			this.Priority = (int)DefaultPriority;

			GenerateCommandID();
		}

		public override void ProcessCommand()
		{
			logger.Info("Processing CommandRequest_TraktSyncCollectionSeries");

			try
			{
                if (!ServerSettings.WebCache_Trakt_Send || string.IsNullOrEmpty(ServerSettings.Trakt_AuthToken)) return;

				AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
				AnimeSeries series = repSeries.GetByID(AnimeSeriesID);
				if (series == null)
				{
					logger.Error("Could not find anime series: {0}", AnimeSeriesID);
					return;
				}

				TraktTVHelper.SyncCollectionToTrakt_Series(series);
			}
			catch (Exception ex)
			{
				logger.Error("Error processing CommandRequest_TraktSyncCollectionSeries: {0}", ex.ToString());
				return;
			}
		}

		/// <summary>
		/// This should generate a unique key for a command
		/// It will be used to check whether the command has already been queued before adding it
		/// </summary>
		public override void GenerateCommandID()
		{
			this.CommandID = string.Format("CommandRequest_TraktSyncCollectionSeries_{0}", AnimeSeriesID);
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
				this.AnimeSeriesID = int.Parse(TryGetProperty(docCreator, "CommandRequest_TraktSyncCollectionSeries", "AnimeSeriesID"));
				this.SeriesName = TryGetProperty(docCreator, "CommandRequest_TraktSyncCollectionSeries", "SeriesName");
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

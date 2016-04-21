using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using JMMServer.Entities;
using JMMServer.Repositories;
using JMMServer.Providers.TvDB;
using System.Collections.Specialized;
using System.Threading;
using System.Globalization;
using System.Configuration;

namespace JMMServer.Commands
{
	[Serializable]
	public class CommandRequest_TvDBDownloadImages : CommandRequestImplementation, ICommandRequest
	{
		public int TvDBSeriesID { get; set; }
		public bool ForceRefresh { get; set; }

		public CommandRequestPriority DefaultPriority 
		{
			get { return CommandRequestPriority.Priority8; }
		}

		public string PrettyDescription
		{
			get
			{
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                return string.Format(JMMServer.Properties.Resources.Command_DownloadTvDBImages, TvDBSeriesID);
			}
		}

		public CommandRequest_TvDBDownloadImages()
		{
		}

		public CommandRequest_TvDBDownloadImages(int tvDBSeriesID, bool forced)
		{
			this.TvDBSeriesID = tvDBSeriesID;
			this.ForceRefresh = forced;
			this.CommandType = (int)CommandRequestType.TvDB_DownloadImages;
			this.Priority = (int)DefaultPriority;

			GenerateCommandID();
		}

		public override void ProcessCommand()
		{
			logger.Info("Processing CommandRequest_TvDBDownloadImages: {0}", TvDBSeriesID);

			try
			{
				JMMService.TvdbHelper.DownloadAutomaticImages(TvDBSeriesID, ForceRefresh);

			}
			catch (Exception ex)
			{
				logger.Error("Error processing CommandRequest_TvDBDownloadImages: {0} - {1}", TvDBSeriesID, ex.ToString());
				return;
			}
		}

		public override void GenerateCommandID()
		{
			this.CommandID = string.Format("CommandRequest_TvDBDownloadImages_{0}", this.TvDBSeriesID);
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
				this.TvDBSeriesID = int.Parse(TryGetProperty(docCreator, "CommandRequest_TvDBDownloadImages", "TvDBSeriesID"));
				this.ForceRefresh = bool.Parse(TryGetProperty(docCreator, "CommandRequest_TvDBDownloadImages", "ForceRefresh"));
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

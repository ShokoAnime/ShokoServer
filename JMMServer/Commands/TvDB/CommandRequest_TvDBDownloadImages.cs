using System;
using System.Globalization;
using System.Threading;
using System.Xml;
using JMMServer.Entities;
using JMMServer.Properties;

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

                return string.Format(Resources.Command_DownloadTvDBImages, TvDBSeriesID);
            }
        }

        /*
		public CommandRequest_TvDBDownloadImages(int tvDBSeriesID, bool forced)
		{
			this.TvDBSeriesID = tvDBSeriesID;
			this.ForceRefresh = forced;
			this.CommandType = (int)CommandRequestType.TvDB_DownloadImages;
			this.Priority = (int)DefaultPriority;

			GenerateCommandID();
        }
        */

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_TvDBDownloadImages: {0}", TvDBSeriesID);

            try
            {
                JMMService.TvdbHelper.DownloadAutomaticImages(TvDBSeriesID, ForceRefresh);
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_TvDBDownloadImages: {0} - {1}", TvDBSeriesID,
                    ex.ToString());
            }
        }

        public override bool LoadFromDBCommand(CommandRequest cq)
        {
            CommandID = cq.CommandID;
            CommandRequestID = cq.CommandRequestID;
            CommandType = cq.CommandType;
            Priority = cq.Priority;
            CommandDetails = cq.CommandDetails;
            DateTimeUpdated = cq.DateTimeUpdated;

            // read xml to get parameters
            if (CommandDetails.Trim().Length > 0)
            {
                var docCreator = new XmlDocument();
                docCreator.LoadXml(CommandDetails);

                // populate the fields
                TvDBSeriesID = int.Parse(TryGetProperty(docCreator, "CommandRequest_TvDBDownloadImages", "TvDBSeriesID"));
                ForceRefresh =
                    bool.Parse(TryGetProperty(docCreator, "CommandRequest_TvDBDownloadImages", "ForceRefresh"));
            }

            return true;
        }

        public override void GenerateCommandID()
        {
            CommandID = string.Format("CommandRequest_TvDBDownloadImages_{0}", TvDBSeriesID);
        }

        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();

            var cq = new CommandRequest();
            cq.CommandID = CommandID;
            cq.CommandType = CommandType;
            cq.Priority = Priority;
            cq.CommandDetails = ToXML();
            cq.DateTimeUpdated = DateTime.Now;

            return cq;
        }
    }
}
using System;
using System.Globalization;
using System.Threading;
using System.Xml;
using JMMServer.Entities;
using JMMServer.Properties;

namespace JMMServer.Commands
{
    [Serializable]
    public class CommandRequest_TvDBUpdateSeriesAndEpisodes : CommandRequestImplementation, ICommandRequest
    {
        public CommandRequest_TvDBUpdateSeriesAndEpisodes()
        {
        }

        public CommandRequest_TvDBUpdateSeriesAndEpisodes(int tvDBSeriesID, bool forced)
        {
            TvDBSeriesID = tvDBSeriesID;
            ForceRefresh = forced;
            CommandType = (int)CommandRequestType.TvDB_SeriesEpisodes;
            Priority = (int)DefaultPriority;

            GenerateCommandID();
        }

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

                return string.Format(Resources.Command_GettingTvDB, TvDBSeriesID);
            }
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_TvDBUpdateSeriesAndEpisodes: {0}", TvDBSeriesID);

            try
            {
                JMMService.TvdbHelper.UpdateAllInfoAndImages(TvDBSeriesID, ForceRefresh, true);
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_TvDBUpdateSeriesAndEpisodes: {0} - {1}", TvDBSeriesID,
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
                TvDBSeriesID =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_TvDBUpdateSeriesAndEpisodes", "TvDBSeriesID"));
                ForceRefresh =
                    bool.Parse(TryGetProperty(docCreator, "CommandRequest_TvDBUpdateSeriesAndEpisodes", "ForceRefresh"));
            }

            return true;
        }

        public override void GenerateCommandID()
        {
            CommandID = string.Format("CommandRequest_TvDBUpdateSeriesAndEpisodes{0}", TvDBSeriesID);
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
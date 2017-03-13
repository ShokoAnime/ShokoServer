using System;
using System.Globalization;
using System.Threading;
using System.Xml;
using Shoko.Models.Server;

namespace Shoko.Server.Commands
{
    [Serializable]
    public class CommandRequest_TvDBUpdateSeriesAndEpisodes : CommandRequestImplementation, ICommandRequest
    {
        public int TvDBSeriesID { get; set; }
        public bool ForceRefresh { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority8; }
        }

        public QueueStateStruct PrettyDescription
        {
            get
            {
                return new QueueStateStruct()
                {
                    queueState = QueueStateEnum.GettingTvDB,
                    extraParams = new string[] {TvDBSeriesID.ToString()}
                };
            }
        }

        public CommandRequest_TvDBUpdateSeriesAndEpisodes()
        {
        }

        public CommandRequest_TvDBUpdateSeriesAndEpisodes(int tvDBSeriesID, bool forced)
        {
            this.TvDBSeriesID = tvDBSeriesID;
            this.ForceRefresh = forced;
            this.CommandType = (int) CommandRequestType.TvDB_SeriesEpisodes;
            this.Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_TvDBUpdateSeriesAndEpisodes: {0}", TvDBSeriesID);

            try
            {
                ShokoService.TvdbHelper.UpdateAllInfoAndImages(TvDBSeriesID, ForceRefresh, true);
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_TvDBUpdateSeriesAndEpisodes: {0} - {1}", TvDBSeriesID,
                    ex.ToString());
                return;
            }
        }

        public override void GenerateCommandID()
        {
            this.CommandID = string.Format("CommandRequest_TvDBUpdateSeriesAndEpisodes{0}", this.TvDBSeriesID);
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
                this.TvDBSeriesID =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_TvDBUpdateSeriesAndEpisodes", "TvDBSeriesID"));
                this.ForceRefresh =
                    bool.Parse(TryGetProperty(docCreator, "CommandRequest_TvDBUpdateSeriesAndEpisodes",
                        "ForceRefresh"));
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
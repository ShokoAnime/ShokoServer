using System;
using System.Globalization;
using System.Threading;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Providers.TvDB;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands
{
    [Serializable]
    public class CommandRequest_TvDBUpdateSeries : CommandRequestImplementation, ICommandRequest
    {
        public int TvDBSeriesID { get; set; }
        public bool ForceRefresh { get; set; }
        public string SeriesTitle { get; set; }

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
                    queueState = QueueStateEnum.GettingTvDBSeries,
                    extraParams = new string[] {$"{SeriesTitle} ({TvDBSeriesID})"}
                };
            }
        }

        public CommandRequest_TvDBUpdateSeries()
        {
        }

        public CommandRequest_TvDBUpdateSeries(int tvDBSeriesID, bool forced)
        {
            this.TvDBSeriesID = tvDBSeriesID;
            this.ForceRefresh = forced;
            this.CommandType = (int) CommandRequestType.TvDB_UpdateSeries;
            this.Priority = (int) DefaultPriority;
            this.SeriesTitle = RepoFactory.TvDB_Series.GetByTvDBID(tvDBSeriesID)?.SeriesName ?? string.Intern("Name not Available");

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_TvDBUpdateSeries: {0}", TvDBSeriesID);

            try
            {
                TvDBApiHelper.UpdateSeriesInfoAndImages(TvDBSeriesID, ForceRefresh, true);
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_TvDBUpdateSeries: {0} - {1}", TvDBSeriesID,
                    ex.ToString());
                return;
            }
        }

        public override void GenerateCommandID()
        {
            this.CommandID = $"CommandRequest_TvDBUpdateSeries{this.TvDBSeriesID}";
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
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_TvDBUpdateSeries", "TvDBSeriesID"));
                this.ForceRefresh =
                    bool.Parse(TryGetProperty(docCreator, "CommandRequest_TvDBUpdateSeries",
                        "ForceRefresh"));
                this.SeriesTitle =
                    TryGetProperty(docCreator, "CommandRequest_TvDBUpdateSeries",
                        "SeriesTitle");
                if (string.IsNullOrEmpty(SeriesTitle))
                {
                    this.SeriesTitle = RepoFactory.TvDB_Series.GetByTvDBID(TvDBSeriesID)?.SeriesName ??
                                       string.Intern("Name not Available");
                }
            }

            return true;
        }

        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();

            CommandRequest cq = new CommandRequest
            {
                CommandID = this.CommandID,
                CommandType = this.CommandType,
                Priority = this.Priority,
                CommandDetails = this.ToXML(),
                DateTimeUpdated = DateTime.Now
            };
            return cq;
        }
    }
}
using System;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Providers.TvDB;
using Shoko.Server.Repositories;
using Shoko.Server.Server;

namespace Shoko.Server.Commands
{
    [Serializable]
    [Command(CommandRequestType.TvDB_UpdateSeries)]
    public class CommandRequest_TvDBUpdateSeries : CommandRequestImplementation
    {
        public int TvDBSeriesID { get; set; }
        public bool ForceRefresh { get; set; }
        public string SeriesTitle { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority6;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.GettingTvDBSeries,
            extraParams = new[] {$"{SeriesTitle} ({TvDBSeriesID})"}
        };

        public CommandRequest_TvDBUpdateSeries()
        {
        }

        public CommandRequest_TvDBUpdateSeries(int tvDBSeriesID, bool forced)
        {
            TvDBSeriesID = tvDBSeriesID;
            ForceRefresh = forced;
            Priority = (int) DefaultPriority;
            SeriesTitle = RepoFactory.TvDB_Series.GetByTvDBID(tvDBSeriesID)?.SeriesName ?? string.Intern("Name not Available");

            GenerateCommandID();
        }

        protected override void Process(IServiceProvider serviceProvider)
        {
            logger.Info("Processing CommandRequest_TvDBUpdateSeries: {0}", TvDBSeriesID);

            try
            {
                TvDBApiHelper.UpdateSeriesInfoAndImages(TvDBSeriesID, ForceRefresh, true);
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_TvDBUpdateSeries: {0} - {1}", TvDBSeriesID,
                    ex);
            }
        }

        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_TvDBUpdateSeries{TvDBSeriesID}";
        }

        public override bool LoadFromDBCommand(CommandRequest cq)
        {
            CommandID = cq.CommandID;
            CommandRequestID = cq.CommandRequestID;
            Priority = cq.Priority;
            CommandDetails = cq.CommandDetails;
            DateTimeUpdated = cq.DateTimeUpdated;

            // read xml to get parameters
            if (CommandDetails.Trim().Length > 0)
            {
                XmlDocument docCreator = new XmlDocument();
                docCreator.LoadXml(CommandDetails);

                // populate the fields
                TvDBSeriesID =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_TvDBUpdateSeries", "TvDBSeriesID"));
                ForceRefresh =
                    bool.Parse(TryGetProperty(docCreator, "CommandRequest_TvDBUpdateSeries",
                        "ForceRefresh"));
                SeriesTitle =
                    TryGetProperty(docCreator, "CommandRequest_TvDBUpdateSeries",
                        "SeriesTitle");
                if (string.IsNullOrEmpty(SeriesTitle))
                {
                    SeriesTitle = RepoFactory.TvDB_Series.GetByTvDBID(TvDBSeriesID)?.SeriesName ??
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
                CommandID = CommandID,
                CommandType = CommandType,
                Priority = Priority,
                CommandDetails = ToXML(),
                DateTimeUpdated = DateTime.Now
            };
            return cq;
        }
    }
}
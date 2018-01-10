using System;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Providers.TvDB;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands
{
    [Serializable]
    public class CommandRequest_TvDBUpdateSeries : CommandRequest_TvDBBase
    {
        public virtual int TvDBSeriesID { get; set; }
        public virtual bool ForceRefresh { get; set; }
        public virtual string SeriesTitle { get; set; }

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
            CommandType = (int) CommandRequestType.TvDB_UpdateSeries;
            Priority = (int) DefaultPriority;
            SeriesTitle = Repo.TvDB_Series.GetByTvDBID(tvDBSeriesID)?.SeriesName ?? string.Intern("Name not Available");

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
                    ex);
            }
        }

        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_TvDBUpdateSeries{TvDBSeriesID}";
        }

        public override bool InitFromDB(CommandRequest cq)
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
                    SeriesTitle = Repo.TvDB_Series.GetByTvDBID(TvDBSeriesID)?.SeriesName ??
                                       string.Intern("Name not Available");
                }
            }

            return true;
        }
    }
}
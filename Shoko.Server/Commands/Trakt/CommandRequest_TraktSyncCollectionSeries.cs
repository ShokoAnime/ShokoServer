using System;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Models;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands
{
    [Serializable]
    public class CommandRequest_TraktSyncCollectionSeries : CommandRequest
    {
        public virtual int AnimeSeriesID { get; set; }
        public virtual string SeriesName { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority9;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.SyncTraktSeries,
            extraParams = new[] {SeriesName}
        };

        public CommandRequest_TraktSyncCollectionSeries()
        {
        }

        public CommandRequest_TraktSyncCollectionSeries(int animeSeriesID, string seriesName)
        {
            AnimeSeriesID = animeSeriesID;
            SeriesName = seriesName;
            CommandType = (int) CommandRequestType.Trakt_SyncCollectionSeries;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_TraktSyncCollectionSeries");

            try
            {
                if (!ServerSettings.Trakt_IsEnabled || string.IsNullOrEmpty(ServerSettings.Trakt_AuthToken)) return;

                SVR_AnimeSeries series = Repo.AnimeSeries.GetByID(AnimeSeriesID);
                if (series == null)
                {
                    logger.Error("Could not find anime series: {0}", AnimeSeriesID);
                    return;
                }

                TraktTVHelper.SyncCollectionToTrakt_Series(series);
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_TraktSyncCollectionSeries: {0}", ex);
            }
        }

        /// <summary>
        /// This should generate a unique key for a command
        /// It will be used to check whether the command has already been queued before adding it
        /// </summary>
        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_TraktSyncCollectionSeries_{AnimeSeriesID}";
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
                AnimeSeriesID =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_TraktSyncCollectionSeries", "AnimeSeriesID"));
                SeriesName = TryGetProperty(docCreator, "CommandRequest_TraktSyncCollectionSeries", "SeriesName");
            }

            return true;
        }
    }
}
using System;
using System.Xml;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Models;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;

namespace Shoko.Server.Commands
{
    [Serializable]
    [Command(CommandRequestType.Trakt_SyncCollectionSeries)]
    public class CommandRequest_TraktSyncCollectionSeries : CommandRequestImplementation
    {
        public int AnimeSeriesID { get; set; }
        public string SeriesName { get; set; }

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
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        protected override void Process(IServiceProvider serviceProvider)
        {
            Logger.LogInformation("Processing CommandRequest_TraktSyncCollectionSeries");

            try
            {
                if (!ServerSettings.Instance.TraktTv.Enabled || string.IsNullOrEmpty(ServerSettings.Instance.TraktTv.AuthToken)) return;

                SVR_AnimeSeries series = RepoFactory.AnimeSeries.GetByID(AnimeSeriesID);
                if (series == null)
                {
                    Logger.LogError("Could not find anime series: {0}", AnimeSeriesID);
                    return;
                }

                TraktTVHelper.SyncCollectionToTrakt_Series(series);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error processing CommandRequest_TraktSyncCollectionSeries: {0}", ex);
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
                AnimeSeriesID =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_TraktSyncCollectionSeries", "AnimeSeriesID"));
                SeriesName = TryGetProperty(docCreator, "CommandRequest_TraktSyncCollectionSeries", "SeriesName");
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
using System;
using System.Globalization;
using System.Threading;
using System.Xml;
using JMMServer.Entities;
using JMMServer.Properties;
using JMMServer.Providers.TraktTV;
using JMMServer.Repositories;

namespace JMMServer.Commands
{
    [Serializable]
    public class CommandRequest_TraktSyncCollectionSeries : CommandRequestImplementation, ICommandRequest
    {
        public CommandRequest_TraktSyncCollectionSeries()
        {
        }

        public CommandRequest_TraktSyncCollectionSeries(int animeSeriesID, string seriesName)
        {
            AnimeSeriesID = animeSeriesID;
            SeriesName = seriesName;
            CommandType = (int)CommandRequestType.Trakt_SyncCollectionSeries;
            Priority = (int)DefaultPriority;

            GenerateCommandID();
        }

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
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                return string.Format(Resources.Command_SyncTraktSeries, SeriesName);
            }
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_TraktSyncCollectionSeries");

            try
            {
                if (!ServerSettings.Trakt_IsEnabled || string.IsNullOrEmpty(ServerSettings.Trakt_AuthToken)) return;

                var repSeries = new AnimeSeriesRepository();
                var series = repSeries.GetByID(AnimeSeriesID);
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
                AnimeSeriesID =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_TraktSyncCollectionSeries", "AnimeSeriesID"));
                SeriesName = TryGetProperty(docCreator, "CommandRequest_TraktSyncCollectionSeries", "SeriesName");
            }

            return true;
        }

        /// <summary>
        ///     This should generate a unique key for a command
        ///     It will be used to check whether the command has already been queued before adding it
        /// </summary>
        public override void GenerateCommandID()
        {
            CommandID = string.Format("CommandRequest_TraktSyncCollectionSeries_{0}", AnimeSeriesID);
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
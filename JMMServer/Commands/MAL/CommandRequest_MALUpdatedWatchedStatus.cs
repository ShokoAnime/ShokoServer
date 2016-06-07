using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Xml;
using JMMServer.Entities;
using JMMServer.Properties;
using JMMServer.Providers.MyAnimeList;
using JMMServer.Repositories;

namespace JMMServer.Commands.MAL
{
    [Serializable]
    public class CommandRequest_MALUpdatedWatchedStatus : CommandRequestImplementation, ICommandRequest
    {
        public CommandRequest_MALUpdatedWatchedStatus()
        {
        }

        public CommandRequest_MALUpdatedWatchedStatus(int animeID)
        {
            AnimeID = animeID;
            CommandType = (int)CommandRequestType.MAL_UpdateStatus;
            Priority = (int)DefaultPriority;

            GenerateCommandID();
        }

        public int AnimeID { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority9; }
        }

        public string PrettyDescription
        {
            get
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                return string.Format(Resources.Command_UpdateMALWatched, AnimeID);
            }
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_MALUpdatedWatchedStatus: {0}", AnimeID);

            try
            {
                // find the latest eps to update
                var repAnime = new AniDB_AnimeRepository();
                var anime = repAnime.GetByAnimeID(AnimeID);
                if (anime == null) return;

                var crossRefs = anime.GetCrossRefMAL();
                if (crossRefs == null || crossRefs.Count == 0)
                    return;

                var repSeries = new AnimeSeriesRepository();
                var ser = repSeries.GetByAnimeID(AnimeID);
                if (ser == null) return;

                MALHelper.UpdateMALSeries(ser);
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_MALUpdatedWatchedStatus: {0} - {1}", AnimeID,
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
                AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_MALUpdatedWatchedStatus", "AnimeID"));
            }

            return true;
        }

        private int GetUpperEpisodeLimit(List<CrossRef_AniDB_MAL> crossRefs, CrossRef_AniDB_MAL xrefBase)
        {
            foreach (var xref in crossRefs)
            {
                if (xref.StartEpisodeType == xrefBase.StartEpisodeType)
                {
                    if (xref.StartEpisodeNumber > xrefBase.StartEpisodeNumber)
                        return xref.StartEpisodeNumber - 1;
                }
            }

            return int.MaxValue;
        }

        public override void GenerateCommandID()
        {
            CommandID = string.Format("CommandRequest_MALUpdatedWatchedStatus_{0}", AnimeID);
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
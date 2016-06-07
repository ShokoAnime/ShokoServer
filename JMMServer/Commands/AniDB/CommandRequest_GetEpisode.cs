using System;
using System.Globalization;
using System.Threading;
using System.Xml;
using JMMServer.Entities;
using JMMServer.Properties;
using JMMServer.Repositories;

namespace JMMServer.Commands.AniDB
{
    [Serializable]
    public class CommandRequest_GetEpisode : CommandRequestImplementation, ICommandRequest
    {
        public CommandRequest_GetEpisode()
        {
        }

        public CommandRequest_GetEpisode(int epID)
        {
            EpisodeID = epID;
            CommandType = (int)CommandRequestType.AniDB_GetEpisodeUDP;
            Priority = (int)DefaultPriority;

            GenerateCommandID();
        }

        public int EpisodeID { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority4; }
        }

        public string PrettyDescription
        {
            get
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                return string.Format(Resources.Command_GetEpisodeList, EpisodeID);
            }
        }

        public override void ProcessCommand()
        {
            logger.Info("Get AniDB episode info: {0}", EpisodeID);


            try
            {
                // we don't use this command to update episode info
                // we actually use it to update the cross ref info instead
                // and we only use it for the "Other Episodes" section of the FILE command
                // because that field doesn't tell you what anime it belongs to

                var repCrossRefs = new CrossRef_File_EpisodeRepository();
                var xrefs = repCrossRefs.GetByEpisodeID(EpisodeID);
                if (xrefs.Count == 0) return;

                var epInfo = JMMService.AnidbProcessor.GetEpisodeInfo(EpisodeID);
                if (epInfo != null)
                {
                    var repSeries = new AnimeSeriesRepository();

                    foreach (var xref in xrefs)
                    {
                        var oldAnimeID = xref.AnimeID;
                        xref.AnimeID = epInfo.AnimeID;
                        repCrossRefs.Save(xref);


                        var ser = repSeries.GetByAnimeID(oldAnimeID);
                        if (ser != null)
                            ser.QueueUpdateStats();
                        StatsCache.Instance.UpdateUsingAnime(oldAnimeID);

                        ser = repSeries.GetByAnimeID(epInfo.AnimeID);
                        if (ser != null)
                            ser.QueueUpdateStats();
                        StatsCache.Instance.UpdateUsingAnime(epInfo.AnimeID);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_GetEpisode: {0} - {1}", EpisodeID, ex.ToString());
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
                EpisodeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_GetEpisode", "EpisodeID"));
            }

            return true;
        }

        /// <summary>
        ///     This should generate a unique key for a command
        ///     It will be used to check whether the command has already been queued before adding it
        /// </summary>
        public override void GenerateCommandID()
        {
            CommandID = string.Format("CommandRequest_GetEpisode_{0}", EpisodeID);
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
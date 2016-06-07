using System;
using System.Globalization;
using System.Threading;
using System.Xml;
using JMMServer.Entities;
using JMMServer.Properties;
using JMMServer.Repositories;

namespace JMMServer.Commands
{
    [Serializable]
    public class CommandRequest_UpdateMyListFileStatus : CommandRequestImplementation, ICommandRequest
    {
        public CommandRequest_UpdateMyListFileStatus()
        {
        }

        public CommandRequest_UpdateMyListFileStatus(string hash, bool watched, bool updateSeriesStats,
            int watchedDateSecs)
        {
            Hash = hash;
            Watched = watched;
            CommandType = (int)CommandRequestType.AniDB_UpdateWatchedUDP;
            Priority = (int)DefaultPriority;
            UpdateSeriesStats = updateSeriesStats;
            WatchedDateAsSecs = watchedDateSecs;

            GenerateCommandID();
        }

        public string FullFileName { get; set; }
        public string Hash { get; set; }
        public bool Watched { get; set; }
        public bool UpdateSeriesStats { get; set; }
        public int WatchedDateAsSecs { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority8; }
        }

        public string PrettyDescription
        {
            get
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                return string.Format(Resources.Command_UpdateMyListInfo, FullFileName);
            }
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_UpdateMyListFileStatus: {0}", Hash);


            try
            {
                var repVids = new VideoLocalRepository();
                var repEpisodes = new AnimeEpisodeRepository();

                // NOTE - we might return more than one VideoLocal record here, if there are duplicates by hash
                var vid = repVids.GetByHash(Hash);
                if (vid != null)
                {
                    var isManualLink = false;
                    var xrefs = vid.EpisodeCrossRefs;
                    if (xrefs.Count > 0)
                        isManualLink = xrefs[0].CrossRefSource != (int)CrossRefSource.AniDB;

                    if (isManualLink)
                    {
                        JMMService.AnidbProcessor.UpdateMyListFileStatus(xrefs[0].AnimeID,
                            xrefs[0].Episode.EpisodeNumber, Watched);
                        logger.Info("Updating file list status (GENERIC): {0} - {1}", vid.ToString(), Watched);
                    }
                    else
                    {
                        if (WatchedDateAsSecs > 0)
                        {
                            var watchedDate = Utils.GetAniDBDateAsDate(WatchedDateAsSecs);
                            JMMService.AnidbProcessor.UpdateMyListFileStatus(vid, Watched, watchedDate);
                        }
                        else
                            JMMService.AnidbProcessor.UpdateMyListFileStatus(vid, Watched, null);
                        logger.Info("Updating file list status: {0} - {1}", vid.ToString(), Watched);
                    }

                    if (UpdateSeriesStats)
                    {
                        // update watched stats
                        var eps = repEpisodes.GetByHash(vid.ED2KHash);
                        if (eps.Count > 0)
                        {
                            // all the eps should belong to the same anime
                            eps[0].GetAnimeSeries().QueueUpdateStats();
                            //eps[0].AnimeSeries.TopLevelAnimeGroup.UpdateStatsFromTopLevel(true, true, false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_UpdateMyListFileStatus: {0} - {1}", Hash, ex.ToString());
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
                Hash = TryGetProperty(docCreator, "CommandRequest_UpdateMyListFileStatus", "Hash");
                Watched = bool.Parse(TryGetProperty(docCreator, "CommandRequest_UpdateMyListFileStatus", "Watched"));

                var sUpStats = TryGetProperty(docCreator, "CommandRequest_UpdateMyListFileStatus", "UpdateSeriesStats");
                var upStats = true;
                if (bool.TryParse(sUpStats, out upStats))
                    UpdateSeriesStats = upStats;

                var dateSecs = 0;
                if (
                    int.TryParse(
                        TryGetProperty(docCreator, "CommandRequest_UpdateMyListFileStatus", "WatchedDateAsSecs"),
                        out dateSecs))
                    WatchedDateAsSecs = dateSecs;
            }

            if (Hash.Trim().Length > 0)
                return true;
            return false;
        }

        /// <summary>
        ///     This should generate a unique key for a command
        ///     It will be used to check whether the command has already been queued before adding it
        /// </summary>
        public override void GenerateCommandID()
        {
            CommandID = string.Format("CommandRequest_UpdateMyListFileStatus_{0}_{1}", Hash, Guid.NewGuid());
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
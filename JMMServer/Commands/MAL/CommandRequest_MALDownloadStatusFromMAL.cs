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
    public class CommandRequest_MALDownloadStatusFromMAL : CommandRequestImplementation, ICommandRequest
    {
        public CommandRequest_MALDownloadStatusFromMAL()
        {
            CommandType = (int)CommandRequestType.MAL_DownloadWatchedStates;
            Priority = (int)DefaultPriority;

            GenerateCommandID();
        }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority9; }
        }

        public string PrettyDescription
        {
            get
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                return string.Format(Resources.Command_DownloadMalWatched);
            }
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_MALDownloadStatusFromMAL");

            try
            {
                if (string.IsNullOrEmpty(ServerSettings.MAL_Username) ||
                    string.IsNullOrEmpty(ServerSettings.MAL_Password))
                    return;

                // find the latest eps to update
                var repAnime = new AniDB_AnimeRepository();

                var mal = MALHelper.GetMALAnimeList();
                if (mal == null) return;
                if (mal.anime == null) return;

                var repCrossRef = new CrossRef_AniDB_MALRepository();
                var repAniEps = new AniDB_EpisodeRepository();
                var repEp = new AnimeEpisodeRepository();

                // find the anidb user
                var repUsers = new JMMUserRepository();
                var aniDBUsers = repUsers.GetAniDBUsers();
                if (aniDBUsers.Count == 0) return;

                var user = aniDBUsers[0];


                foreach (var malAnime in mal.anime)
                {
                    // look up the anime
                    var xref = repCrossRef.GetByMALID(malAnime.series_animedb_id);
                    if (xref == null) continue;

                    if (malAnime.series_animedb_id == 8107 || malAnime.series_animedb_id == 10737)
                    {
                        Console.Write("");
                    }

                    // check if this anime has any other links
                    var allXrefs = repCrossRef.GetByAnimeID(xref.AnimeID);
                    if (allXrefs.Count == 0) continue;

                    // find the range of watched episodes that this applies to
                    var startEpNumber = xref.StartEpisodeNumber;
                    var endEpNumber = GetUpperEpisodeLimit(allXrefs, xref);

                    var aniEps = repAniEps.GetByAnimeID(xref.AnimeID);
                    foreach (var aniep in aniEps)
                    {
                        if (aniep.EpisodeType != xref.StartEpisodeType) continue;

                        var ep = repEp.GetByAniDBEpisodeID(aniep.EpisodeID);
                        if (ep == null) continue;

                        var adjustedWatchedEps = malAnime.my_watched_episodes + xref.StartEpisodeNumber - 1;
                        var epNum = aniep.EpisodeNumber;

                        if (epNum < startEpNumber || epNum > endEpNumber) continue;

                        var usrRec = ep.GetUserRecord(user.JMMUserID);

                        if (epNum <= adjustedWatchedEps)
                        {
                            // update if the user doesn't have a record (means not watched)
                            // or it is currently un-watched
                            var update = false;
                            if (usrRec == null) update = true;
                            else
                            {
                                if (!usrRec.WatchedDate.HasValue) update = true;
                            }

                            if (update) ep.ToggleWatchedStatus(true, true, DateTime.Now, user.JMMUserID, false);
                        }
                        else
                        {
                            var update = false;
                            if (usrRec != null)
                            {
                                if (usrRec.WatchedDate.HasValue) update = true;
                            }

                            if (update) ep.ToggleWatchedStatus(false, true, DateTime.Now, user.JMMUserID, false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_MALDownloadStatusFromMAL: {0}", ex.ToString());
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
            CommandID = "CommandRequest_MALDownloadStatusFromMAL";
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
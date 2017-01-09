using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Xml;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Direct;
using Shoko.Models.Server;
using Shoko.Server.Entities;
using Shoko.Server.Providers.MyAnimeList;
using Shoko.Server.Repositories;


namespace Shoko.Server.Commands.MAL
{
    [Serializable]
    public class CommandRequest_MALDownloadStatusFromMAL : CommandRequestImplementation, ICommandRequest
    {
        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority9; }
        }

        public QueueStateStruct PrettyDescription
        {
            get
            {
                return new QueueStateStruct() { queueState = QueueStateEnum.DownloadMalWatched, extraParams = new string[0] };
            }
        }


        public CommandRequest_MALDownloadStatusFromMAL()
        {
            this.CommandType = (int) CommandRequestType.MAL_DownloadWatchedStates;
            this.Priority = (int) DefaultPriority;

            GenerateCommandID();
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

                myanimelist mal = MALHelper.GetMALAnimeList();
                if (mal == null) return;
                if (mal.anime == null) return;


                // find the anidb user
                List<SVR_JMMUser> aniDBUsers = RepoFactory.JMMUser.GetAniDBUsers();
                if (aniDBUsers.Count == 0) return;

                SVR_JMMUser user = aniDBUsers[0];


                foreach (myanimelistAnime malAnime in mal.anime)
                {
                    // look up the anime
                    CrossRef_AniDB_MAL xref = RepoFactory.CrossRef_AniDB_MAL.GetByMALID(malAnime.series_animedb_id);
                    if (xref == null) continue;

                    if (malAnime.series_animedb_id == 8107 || malAnime.series_animedb_id == 10737)
                    {
                        Console.Write("");
                    }

                    // check if this anime has any other links
                    List<CrossRef_AniDB_MAL> allXrefs = RepoFactory.CrossRef_AniDB_MAL.GetByAnimeID(xref.AnimeID);
                    if (allXrefs.Count == 0) continue;

                    // find the range of watched episodes that this applies to
                    int startEpNumber = xref.StartEpisodeNumber;
                    int endEpNumber = GetUpperEpisodeLimit(allXrefs, xref);

                    List<SVR_AniDB_Episode> aniEps = RepoFactory.AniDB_Episode.GetByAnimeID(xref.AnimeID);
                    foreach (SVR_AniDB_Episode aniep in aniEps)
                    {
                        if (aniep.EpisodeType != xref.StartEpisodeType) continue;

                        SVR_AnimeEpisode ep = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(aniep.EpisodeID);
                        if (ep == null) continue;

                        int adjustedWatchedEps = malAnime.my_watched_episodes + xref.StartEpisodeNumber - 1;
                        int epNum = aniep.EpisodeNumber;

                        if (epNum < startEpNumber || epNum > endEpNumber) continue;

                        SVR_AnimeEpisode_User usrRec = ep.GetUserRecord(user.JMMUserID);

                        if (epNum <= adjustedWatchedEps)
                        {
                            // update if the user doesn't have a record (means not watched)
                            // or it is currently un-watched
                            bool update = false;
                            if (usrRec == null) update = true;
                            else
                            {
                                if (!usrRec.WatchedDate.HasValue) update = true;
                            }

                            if (update) ep.ToggleWatchedStatus(true, true, DateTime.Now, user.JMMUserID, false);
                        }
                        else
                        {
                            bool update = false;
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
                return;
            }
        }

        private int GetUpperEpisodeLimit(List<CrossRef_AniDB_MAL> crossRefs, CrossRef_AniDB_MAL xrefBase)
        {
            foreach (CrossRef_AniDB_MAL xref in crossRefs)
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
            this.CommandID = string.Format("CommandRequest_MALDownloadStatusFromMAL");
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
            }

            return true;
        }

        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();

            CommandRequest cq = new CommandRequest();
            cq.CommandID = this.CommandID;
            cq.CommandType = this.CommandType;
            cq.Priority = this.Priority;
            cq.CommandDetails = this.ToXML();
            cq.DateTimeUpdated = DateTime.Now;

            return cq;
        }
    }
}
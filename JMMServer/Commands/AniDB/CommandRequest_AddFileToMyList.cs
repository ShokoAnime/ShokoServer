using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Xml;
using JMMServer.Commands.MAL;
using JMMServer.Entities;
using JMMServer.Repositories;

namespace JMMServer.Commands
{
    [Serializable]
    public class CommandRequest_AddFileToMyList : CommandRequestImplementation, ICommandRequest
    {
        public string Hash { get; set; }

        private VideoLocal vid = null;

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority9; }
        }

        public string PrettyDescription
        {
            get
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                if (vid != null)
                    return string.Format(JMMServer.Properties.Resources.AniDB_MyListAdd, vid.FullServerPath);
                else
                    return string.Format(JMMServer.Properties.Resources.AniDB_MyListAdd, Hash);
            }
        }

        public CommandRequest_AddFileToMyList()
        {
        }

        public CommandRequest_AddFileToMyList(string hash)
        {
            this.Hash = hash;
            this.CommandType = (int) CommandRequestType.AniDB_AddFileUDP;
            this.Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_AddFileToMyList: {0}", Hash);


            try
            {
                VideoLocalRepository repVids = new VideoLocalRepository();
                AnimeEpisodeRepository repEpisodes = new AnimeEpisodeRepository();

                vid = repVids.GetByHash(this.Hash);
                List<AnimeEpisode> animeEpisodes = new List<AnimeEpisode>();
                if (vid != null) animeEpisodes = vid.GetAnimeEpisodes();

                if (vid != null)
                {
                    // when adding a file via the API, newWatchedStatus will return with current watched status on AniDB
                    // if the file is already on the user's list

                    bool isManualLink = false;
                    List<CrossRef_File_Episode> xrefs = vid.EpisodeCrossRefs;
                    if (xrefs.Count > 0)
                        isManualLink = xrefs[0].CrossRefSource != (int) CrossRefSource.AniDB;

                    // mark the video file as watched
                    DateTime? watchedDate = null;
                    bool newWatchedStatus = false;

                    if (isManualLink)
                        newWatchedStatus = JMMService.AnidbProcessor.AddFileToMyList(xrefs[0].AnimeID,
                            xrefs[0].Episode.EpisodeNumber, ref watchedDate);
                    else
                        newWatchedStatus = JMMService.AnidbProcessor.AddFileToMyList(vid, ref watchedDate);

                    // do for all AniDB users
                    JMMUserRepository repUsers = new JMMUserRepository();
                    List<JMMUser> aniDBUsers = repUsers.GetAniDBUsers();


                    if (aniDBUsers.Count > 0)
                    {
                        JMMUser juser = aniDBUsers[0];
                        vid.ToggleWatchedStatus(newWatchedStatus, false, watchedDate, false, false, juser.JMMUserID,
                            false, true);
                        logger.Info("Adding file to list: {0} - {1}", vid.ToString(), watchedDate);

                        // if the the episode is watched we may want to set the file to watched as well
                        if (ServerSettings.Import_UseExistingFileWatchedStatus && !newWatchedStatus)
                        {
                            if (animeEpisodes.Count > 0)
                            {
                                AnimeEpisode ep = animeEpisodes[0];
                                AnimeEpisode_User epUser = null;

                                foreach (JMMUser tempuser in aniDBUsers)
                                {
                                    // only find the first user who watched this
                                    if (epUser == null)
                                        epUser = ep.GetUserRecord(tempuser.JMMUserID);
                                }

                                if (epUser != null)
                                {
                                    logger.Info(
                                        "Setting file as watched, because episode was already watched: {0} - user: {1}",
                                        vid.ToString(), juser.Username);
                                    vid.ToggleWatchedStatus(true, true, epUser.WatchedDate, false, false,
                                        epUser.JMMUserID, false, true);
                                }
                            }
                        }
                    }

                    AnimeSeries ser = animeEpisodes[0].GetAnimeSeries();
                    // all the eps should belong to the same anime
                    ser.QueueUpdateStats();
                    //StatsCache.Instance.UpdateUsingSeries(ser.AnimeSeriesID);

                    // lets also try adding to the users trakt collecion
                    if (ser != null && ServerSettings.Trakt_IsEnabled &&
                        !string.IsNullOrEmpty(ServerSettings.Trakt_AuthToken))
                    {
                        foreach (AnimeEpisode aep in animeEpisodes)
                        {
                            CommandRequest_TraktCollectionEpisode cmdSyncTrakt =
                                new CommandRequest_TraktCollectionEpisode(aep.AnimeEpisodeID, TraktSyncAction.Add);
                            cmdSyncTrakt.Save();
                        }
                    }

                    // sync the series on MAL
                    if (ser != null && !string.IsNullOrEmpty(ServerSettings.MAL_Username) &&
                        !string.IsNullOrEmpty(ServerSettings.MAL_Password))
                    {
                        CommandRequest_MALUpdatedWatchedStatus cmdMAL =
                            new CommandRequest_MALUpdatedWatchedStatus(ser.AniDB_ID);
                        cmdMAL.Save();
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_AddFileToMyList: {0} - {1}", Hash, ex.ToString());
                return;
            }
        }

        /// <summary>
        /// This should generate a unique key for a command
        /// It will be used to check whether the command has already been queued before adding it
        /// </summary>
        public override void GenerateCommandID()
        {
            this.CommandID = string.Format("CommandRequest_AddFileToMyList_{0}", Hash);
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

                // populate the fields
                this.Hash = TryGetProperty(docCreator, "CommandRequest_AddFileToMyList", "Hash");
            }

            if (this.Hash.Trim().Length > 0)
                return true;
            else
                return false;
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
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Xml;
using JMMServer.Commands.MAL;
using JMMServer.Entities;
using JMMServer.Properties;
using JMMServer.Repositories;

namespace JMMServer.Commands
{
    [Serializable]
    public class CommandRequest_AddFileToMyList : CommandRequestImplementation, ICommandRequest
    {
        private VideoLocal vid;

        public CommandRequest_AddFileToMyList()
        {
        }

        public CommandRequest_AddFileToMyList(string hash)
        {
            Hash = hash;
            CommandType = (int)CommandRequestType.AniDB_AddFileUDP;
            Priority = (int)DefaultPriority;

            GenerateCommandID();
        }

        public string Hash { get; set; }

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
                    return string.Format(Resources.AniDB_MyListAdd, vid.FullServerPath);
                return string.Format(Resources.AniDB_MyListAdd, Hash);
            }
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_AddFileToMyList: {0}", Hash);


            try
            {
                var repVids = new VideoLocalRepository();
                var repEpisodes = new AnimeEpisodeRepository();

                vid = repVids.GetByHash(Hash);
                var animeEpisodes = new List<AnimeEpisode>();
                if (vid != null) animeEpisodes = vid.GetAnimeEpisodes();

                if (vid != null)
                {
                    // when adding a file via the API, newWatchedStatus will return with current watched status on AniDB
                    // if the file is already on the user's list

                    var isManualLink = false;
                    var xrefs = vid.EpisodeCrossRefs;
                    if (xrefs.Count > 0)
                        isManualLink = xrefs[0].CrossRefSource != (int)CrossRefSource.AniDB;

                    // mark the video file as watched
                    DateTime? watchedDate = null;
                    var newWatchedStatus = false;

                    if (isManualLink)
                        newWatchedStatus = JMMService.AnidbProcessor.AddFileToMyList(xrefs[0].AnimeID,
                            xrefs[0].Episode.EpisodeNumber, ref watchedDate);
                    else
                        newWatchedStatus = JMMService.AnidbProcessor.AddFileToMyList(vid, ref watchedDate);

                    // do for all AniDB users
                    var repUsers = new JMMUserRepository();
                    var aniDBUsers = repUsers.GetAniDBUsers();


                    if (aniDBUsers.Count > 0)
                    {
                        var juser = aniDBUsers[0];
                        vid.ToggleWatchedStatus(newWatchedStatus, false, watchedDate, false, false, juser.JMMUserID,
                            false, true);
                        logger.Info("Adding file to list: {0} - {1}", vid.ToString(), watchedDate);

                        // if the the episode is watched we may want to set the file to watched as well
                        if (ServerSettings.Import_UseExistingFileWatchedStatus && !newWatchedStatus)
                        {
                            if (animeEpisodes.Count > 0)
                            {
                                var ep = animeEpisodes[0];
                                AnimeEpisode_User epUser = null;

                                foreach (var tempuser in aniDBUsers)
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

                    var ser = animeEpisodes[0].GetAnimeSeries();
                    // all the eps should belong to the same anime
                    ser.QueueUpdateStats();
                    //StatsCache.Instance.UpdateUsingSeries(ser.AnimeSeriesID);

                    // lets also try adding to the users trakt collecion
                    if (ser != null && ServerSettings.Trakt_IsEnabled &&
                        !string.IsNullOrEmpty(ServerSettings.Trakt_AuthToken))
                    {
                        foreach (var aep in animeEpisodes)
                        {
                            var cmdSyncTrakt = new CommandRequest_TraktCollectionEpisode(aep.AnimeEpisodeID,
                                TraktSyncAction.Add);
                            cmdSyncTrakt.Save();
                        }
                    }

                    // sync the series on MAL
                    if (ser != null && !string.IsNullOrEmpty(ServerSettings.MAL_Username) &&
                        !string.IsNullOrEmpty(ServerSettings.MAL_Password))
                    {
                        var cmdMAL = new CommandRequest_MALUpdatedWatchedStatus(ser.AniDB_ID);
                        cmdMAL.Save();
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_AddFileToMyList: {0} - {1}", Hash, ex.ToString());
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
                Hash = TryGetProperty(docCreator, "CommandRequest_AddFileToMyList", "Hash");
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
            CommandID = string.Format("CommandRequest_AddFileToMyList_{0}", Hash);
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
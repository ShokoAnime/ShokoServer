using System;
using System.Collections.Generic;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.MAL;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands
{
    [Serializable]
    public class CommandRequest_AddFileToMyList : CommandRequestImplementation, ICommandRequest
    {
        public string Hash { get; set; }

        [NonSerialized]
        private SVR_VideoLocal vid;

        public CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority9;

        public QueueStateStruct PrettyDescription
        {
            get
            {
                if (vid != null)
                    return new QueueStateStruct
                    {
                        queueState = QueueStateEnum.AniDB_MyListAdd,
                        extraParams = new[] {vid.FileName}
                    };
                return new QueueStateStruct
                {
                    queueState = QueueStateEnum.AniDB_MyListAdd,
                    extraParams = new[] {Hash}
                };
            }
        }

        public CommandRequest_AddFileToMyList()
        {
        }

        public CommandRequest_AddFileToMyList(string hash)
        {
            Hash = hash;
            CommandType = (int) CommandRequestType.AniDB_AddFileUDP;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            logger.Info($"Processing CommandRequest_AddFileToMyList: {vid.FileName} - {vid.Hash}");


            try
            {
                if (vid == null) return;

                List<SVR_AnimeEpisode> animeEpisodes = vid.GetAnimeEpisodes();
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
                    newWatchedStatus = ShokoService.AnidbProcessor.AddFileToMyList(xrefs[0].AnimeID,
                        xrefs[0].GetEpisode().EpisodeNumber,
                        ref watchedDate);
                else
                    newWatchedStatus = ShokoService.AnidbProcessor.AddFileToMyList(vid, ref watchedDate);

                // do for all AniDB users
                List<SVR_JMMUser> aniDBUsers = RepoFactory.JMMUser.GetAniDBUsers();


                if (aniDBUsers.Count > 0)
                {
                    SVR_JMMUser juser = aniDBUsers[0];
                    vid.ToggleWatchedStatus(newWatchedStatus, false, watchedDate, false, false, juser.JMMUserID,
                        false, true);
                    logger.Info($"Adding file to list: {vid.FileName} - {watchedDate}");

                    // if the the episode is watched we may want to set the file to watched as well
                    if (ServerSettings.Import_UseExistingFileWatchedStatus && !newWatchedStatus)
                    {
                        if (animeEpisodes.Count > 0)
                        {
                            SVR_AnimeEpisode ep = animeEpisodes[0];
                            SVR_AnimeEpisode_User epUser = null;

                            foreach (SVR_JMMUser tempuser in aniDBUsers)
                            {
                                // only find the first user who watched this
                                if (epUser == null)
                                    epUser = ep.GetUserRecord(tempuser.JMMUserID);
                            }

                            if (epUser != null)
                            {
                                logger.Info(
                                    $"Setting file as watched, because episode was already watched: {vid.FileName} - user: {epUser.JMMUserID}");
                                vid.ToggleWatchedStatus(true, true, epUser.WatchedDate, false, false,
                                    epUser.JMMUserID, false, true);
                            }
                        }
                    }
                }

                SVR_AnimeSeries ser = animeEpisodes[0].GetAnimeSeries();
                // all the eps should belong to the same anime
                ser.QueueUpdateStats();
                //StatsCache.Instance.UpdateUsingSeries(ser.AnimeSeriesID);

                // lets also try adding to the users trakt collecion
                if (ServerSettings.Trakt_IsEnabled &&
                    !string.IsNullOrEmpty(ServerSettings.Trakt_AuthToken))
                {
                    foreach (SVR_AnimeEpisode aep in animeEpisodes)
                    {
                        CommandRequest_TraktCollectionEpisode cmdSyncTrakt =
                            new CommandRequest_TraktCollectionEpisode
                            (
                                aep.AnimeEpisodeID, TraktSyncAction.Add);
                        cmdSyncTrakt.Save();
                    }
                }

                // sync the series on MAL
                if (!string.IsNullOrEmpty(ServerSettings.MAL_Username) &&
                    !string.IsNullOrEmpty(ServerSettings.MAL_Password))
                {
                    CommandRequest_MALUpdatedWatchedStatus cmdMAL =
                        new CommandRequest_MALUpdatedWatchedStatus(ser.AniDB_ID);
                    cmdMAL.Save();
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error processing CommandRequest_AddFileToMyList: {Hash} - {ex}");
            }
        }

        /// <summary>
        /// This should generate a unique key for a command
        /// It will be used to check whether the command has already been queued before adding it
        /// </summary>
        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_AddFileToMyList_{Hash}";
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
                XmlDocument docCreator = new XmlDocument();
                docCreator.LoadXml(CommandDetails);

                // populate the fields
                Hash = TryGetProperty(docCreator, "CommandRequest_AddFileToMyList", "Hash");
            }

            if (Hash.Trim().Length <= 0) return false;
            vid = RepoFactory.VideoLocal.GetByHash(Hash);
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
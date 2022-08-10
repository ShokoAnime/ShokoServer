using System;
using System.Linq;
using System.Xml;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Generic;
using Shoko.Server.Providers.AniDB.UDP.User;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;

namespace Shoko.Server.Commands.AniDB
{
    [Serializable]
    [Command(CommandRequestType.AniDB_AddFileUDP)]
    public class CommandRequest_AddFileToMyList : CommandRequestImplementation
    {
        public string Hash { get; set; }
        public bool ReadStates { get; set; } = true;


        [NonSerialized]
        private SVR_VideoLocal vid;

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority6;

        public override QueueStateStruct PrettyDescription
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

        public CommandRequest_AddFileToMyList(string hash, bool readstate = true)
        {
            Hash = hash;
            ReadStates = readstate;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        protected override void Process(IServiceProvider serviceProvider)
        {
            logger.Info($"Processing CommandRequest_AddFileToMyList: {vid?.GetBestVideoLocalPlace()?.FileName} - {Hash} - {ReadStates}");

            try
            {
                if (vid == null) return;
                var handler = serviceProvider.GetRequiredService<IUDPConnectionHandler>();

                // when adding a file via the API, newWatchedStatus will return with current watched status on AniDB
                // if the file is already on the user's list

                var isManualLink = vid.GetAniDBFile() == null;

                // mark the video file as watched
                var aniDBUsers = RepoFactory.JMMUser.GetAniDBUsers();
                var juser = aniDBUsers.FirstOrDefault();
                DateTime? originalWatchedDate = null;
                if (juser != null)
                    originalWatchedDate = vid.GetUserRecord(juser.JMMUserID)?.WatchedDate?.ToUniversalTime();

                UDPResponse<ResponseMyListFile> response = null;
                // this only gets overwritten if the response is File Already in MyList
                var state = ServerSettings.Instance.AniDb.MyList_StorageState;

                if (isManualLink)
                {
                    var episodes = vid.GetAnimeEpisodes().Select(a => a.AniDB_Episode).ToArray();
                    foreach (var episode in episodes)
                    {
                        var request = new RequestAddEpisode
                        {
                            State = state.GetMyList_State(),
                            IsWatched = originalWatchedDate.HasValue,
                            WatchedDate = originalWatchedDate,
                            AnimeID = episode.AnimeID,
                            EpisodeNumber = episode.EpisodeNumber,
                            EpisodeType = (EpisodeType)episode.EpisodeType,
                        };
                        response = request.Execute(handler);

                        if (response.Code != UDPReturnCode.FILE_ALREADY_IN_MYLIST) continue;
                        var updateRequest = new RequestUpdateEpisode
                        {
                            State = state.GetMyList_State(),
                            IsWatched = originalWatchedDate.HasValue,
                            WatchedDate = originalWatchedDate,
                            AnimeID = episode.AnimeID,
                            EpisodeNumber = episode.EpisodeNumber,
                            EpisodeType = (EpisodeType)episode.EpisodeType,
                        };
                        updateRequest.Execute(handler);
                    }
                }
                else
                {
                    var request = new RequestAddFile
                    {
                        State = state.GetMyList_State(),
                        IsWatched = originalWatchedDate.HasValue,
                        WatchedDate = originalWatchedDate,
                        Hash = vid.Hash,
                        Size = vid.FileSize,
                    };
                    response = request.Execute(handler);

                    if (response.Code == UDPReturnCode.FILE_ALREADY_IN_MYLIST)
                    {
                        var updateRequest = new RequestUpdateFile
                        {
                            State = state.GetMyList_State(),
                            IsWatched = originalWatchedDate.HasValue,
                            WatchedDate = originalWatchedDate,
                            Hash = vid.Hash,
                            Size = vid.FileSize,
                        };
                        updateRequest.Execute(handler);
                    }
                }

                // never true for Manual Links, so no worries about the loop overwriting it
                if ((response?.Response?.MyListID ?? 0) != 0)
                {
                    vid.MyListID = response.Response.MyListID;
                    RepoFactory.VideoLocal.Save(vid);
                }

                var newWatchedDate = response?.Response?.WatchedDate;
                logger.Info($"Added File to MyList. File: {vid.GetBestVideoLocalPlace()?.FileName}  Manual Link: {isManualLink}  Watched Locally: {originalWatchedDate != null}  Watched AniDB: {response?.Response?.IsWatched}  Local State: {ServerSettings.Instance.AniDb.MyList_StorageState}  AniDB State: {state}  ReadStates: {ReadStates}  ReadWatched Setting: {ServerSettings.Instance.AniDb.MyList_ReadWatched}  ReadUnwatched Setting: {ServerSettings.Instance.AniDb.MyList_ReadUnwatched}");
                if (juser != null)
                {
                    var watched = newWatchedDate != null && DateTime.UnixEpoch.Equals(newWatchedDate);
                    var watchedLocally = originalWatchedDate != null;

                    if (ReadStates)
                    {
                        // handle import watched settings. Don't update AniDB in either case, we'll do that with the storage state
                        if (ServerSettings.Instance.AniDb.MyList_ReadWatched && watched && !watchedLocally)
                        {
                            vid.ToggleWatchedStatus(true, false, newWatchedDate, false, juser.JMMUserID,
                                false, false);
                        }
                        else if (ServerSettings.Instance.AniDb.MyList_ReadUnwatched && !watched && watchedLocally)
                        {
                            vid.ToggleWatchedStatus(false, false, null, false, juser.JMMUserID,
                                false, false);
                        }
                    }
                }

                // if we don't have xrefs, then no series or eps.
                var series = vid.EpisodeCrossRefs.Select(a => a.AnimeID).Distinct().ToArray();
                if (series.Length <= 0) return;

                foreach (var id in series)
                {
                    var ser = RepoFactory.AnimeSeries.GetByAnimeID(id);
                    ser?.QueueUpdateStats();
                }

                // lets also try adding to the users trakt collection
                if (ServerSettings.Instance.TraktTv.Enabled &&
                    !string.IsNullOrEmpty(ServerSettings.Instance.TraktTv.AuthToken))
                {
                    foreach (var aep in vid.GetAnimeEpisodes())
                    {
                        var cmdSyncTrakt =
                            new CommandRequest_TraktCollectionEpisode(aep.AnimeEpisodeID, TraktSyncAction.Add);
                        cmdSyncTrakt.Save();
                    }
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
                var read = TryGetProperty(docCreator, "CommandRequest_AddFileToMyList", "ReadStates");
                if (!bool.TryParse(read, out var readStates)) readStates = true;
                ReadStates = readStates;
            }

            if (Hash.Trim().Length <= 0) return false;
            vid = RepoFactory.VideoLocal.GetByHash(Hash);
            return true;
        }

        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();

            var cq = new CommandRequest
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

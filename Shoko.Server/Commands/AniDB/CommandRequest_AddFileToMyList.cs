using System;
using System.Linq;
using System.Xml;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Generic;
using Shoko.Server.Providers.AniDB.UDP.User;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;

namespace Shoko.Server.Commands.AniDB;

[Serializable]
[Command(CommandRequestType.AniDB_AddFileUDP)]
public class CommandRequest_AddFileToMyList : CommandRequestImplementation
{
    private readonly IRequestFactory _requestFactory;
    private readonly ICommandRequestFactory _commandFactory;
    private readonly ISettingsProvider _settingsProvider;

    public string Hash { get; set; }
    public bool ReadStates { get; set; } = true;


    [NonSerialized] private SVR_VideoLocal _videoLocal;

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority6;

    public override QueueStateStruct PrettyDescription
    {
        get
        {
            if (_videoLocal != null)
            {
                return new QueueStateStruct
                {
                    message = "Adding file to MyList: {0}",
                    queueState = QueueStateEnum.AniDB_MyListAdd,
                    extraParams = new[] { _videoLocal.FileName }
                };
            }

            return new QueueStateStruct
            {
                message = "Adding file to MyList: {0}",
                queueState = QueueStateEnum.AniDB_MyListAdd,
                extraParams = new[] { Hash }
            };
        }
    }

    protected override void Process()
    {
        Logger.LogInformation("Processing CommandRequest_AddFileToMyList: {FileName} - {Hash} - {ReadStates}",
            _videoLocal?.GetBestVideoLocalPlace()?.FileName, Hash, ReadStates);

        if (_videoLocal == null) return;

        var settings = _settingsProvider.GetSettings();

        // when adding a file via the API, newWatchedStatus will return with current watched status on AniDB
        // if the file is already on the user's list

        var isManualLink = _videoLocal.GetAniDBFile() == null;

        // mark the video file as watched
        var aniDBUsers = RepoFactory.JMMUser.GetAniDBUsers();
        var juser = aniDBUsers.FirstOrDefault();
        DateTime? originalWatchedDate = null;
        if (juser != null)
        {
            originalWatchedDate = _videoLocal.GetUserRecord(juser.JMMUserID)?.WatchedDate?.ToUniversalTime();
        }

        UDPResponse<ResponseMyListFile> response = null;
        // this only gets overwritten if the response is File Already in MyList
        var state = settings.AniDb.MyList_StorageState;

        if (isManualLink)
        {
            var episodes = _videoLocal.GetAnimeEpisodes().Select(a => a.AniDB_Episode).ToArray();
            foreach (var episode in episodes)
            {
                var request = _requestFactory.Create<RequestAddEpisode>(
                    r =>
                    {
                        r.State = state.GetMyList_State();
                        r.IsWatched = originalWatchedDate.HasValue;
                        r.WatchedDate = originalWatchedDate;
                        r.AnimeID = episode.AnimeID;
                        r.EpisodeNumber = episode.EpisodeNumber;
                        r.EpisodeType = (EpisodeType)episode.EpisodeType;
                    }
                );
                response = request.Execute();

                if (response.Code != UDPReturnCode.FILE_ALREADY_IN_MYLIST)
                {
                    continue;
                }

                var updateRequest = _requestFactory.Create<RequestUpdateEpisode>(
                    r =>
                    {
                        r.State = state.GetMyList_State();
                        r.IsWatched = originalWatchedDate.HasValue;
                        r.WatchedDate = originalWatchedDate;
                        r.AnimeID = episode.AnimeID;
                        r.EpisodeNumber = episode.EpisodeNumber;
                        r.EpisodeType = (EpisodeType)episode.EpisodeType;
                    }
                );
                updateRequest.Execute();
            }
        }
        else
        {
            var request = _requestFactory.Create<RequestAddFile>(
                r =>
                {
                    r.State = state.GetMyList_State();
                    r.IsWatched = originalWatchedDate.HasValue;
                    r.WatchedDate = originalWatchedDate;
                    r.Hash = _videoLocal.Hash;
                    r.Size = _videoLocal.FileSize;
                }
            );
            response = request.Execute();

            if (response.Code == UDPReturnCode.FILE_ALREADY_IN_MYLIST)
            {
                var updateRequest = _requestFactory.Create<RequestUpdateFile>(
                    r =>
                    {
                        r.State = state.GetMyList_State();
                        if (originalWatchedDate.HasValue)
                        {
                            r.IsWatched = originalWatchedDate.HasValue;
                            r.WatchedDate = originalWatchedDate;                                
                        }
                        r.Hash = _videoLocal.Hash;
                        r.Size = _videoLocal.FileSize;
                    }
                );
                updateRequest.Execute();
            }
        }

        // never true for Manual Links, so no worries about the loop overwriting it
        if ((response?.Response?.MyListID ?? 0) != 0)
        {
            _videoLocal.MyListID = response.Response.MyListID;
            RepoFactory.VideoLocal.Save(_videoLocal);
        }

        var newWatchedDate = response?.Response?.WatchedDate;
        Logger.LogInformation(
            "Added File to MyList. File: {FileName}  Manual Link: {IsManualLink}  Watched Locally: {Unknown}  Watched AniDB: {ResponseIsWatched}  Local State: {AniDbMyListStorageState}  AniDB State: {State}  ReadStates: {ReadStates}  ReadWatched Setting: {AniDbMyListReadWatched}  ReadUnwatched Setting: {AniDbMyListReadUnwatched}",
            _videoLocal.GetBestVideoLocalPlace()?.FileName, isManualLink, originalWatchedDate != null,
            response?.Response?.IsWatched, settings.AniDb.MyList_StorageState, state, ReadStates,
            settings.AniDb.MyList_ReadWatched, settings.AniDb.MyList_ReadUnwatched
        );
        if (juser != null)
        {
            var watched = newWatchedDate != null && !DateTime.UnixEpoch.Equals(newWatchedDate);
            var watchedLocally = originalWatchedDate != null;

            if (ReadStates)
            {
                // handle import watched settings. Don't update AniDB in either case, we'll do that with the storage state
                if (settings.AniDb.MyList_ReadWatched && watched && !watchedLocally)
                {
                    _videoLocal.ToggleWatchedStatus(true, false, newWatchedDate?.ToLocalTime(), false, juser.JMMUserID,
                        false, false);
                }
                else if (settings.AniDb.MyList_ReadUnwatched && !watched && watchedLocally)
                {
                    _videoLocal.ToggleWatchedStatus(false, false, null, false, juser.JMMUserID,
                        false, false);
                }
            }
        }

        // if we don't have xrefs, then no series or eps.
        var series = _videoLocal.EpisodeCrossRefs.Select(a => a.AnimeID).Distinct().ToArray();
        if (series.Length <= 0)
        {
            return;
        }

        foreach (var id in series)
        {
            var ser = RepoFactory.AnimeSeries.GetByAnimeID(id);
            ser?.QueueUpdateStats();
        }

        // lets also try adding to the users trakt collection
        if (settings.TraktTv.Enabled &&
            !string.IsNullOrEmpty(settings.TraktTv.AuthToken))
        {
            foreach (var aep in _videoLocal.GetAnimeEpisodes())
            {
                var cmdSyncTrakt = _commandFactory.Create<CommandRequest_TraktCollectionEpisode>(
                    c =>
                    {
                        c.AnimeEpisodeID = aep.AnimeEpisodeID;
                        c.Action = (int)TraktSyncAction.Add;
                    }
                );
                cmdSyncTrakt.Save();
            }
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
            if (!bool.TryParse(read, out var readStates))
            {
                readStates = true;
            }

            ReadStates = readStates;
        }

        if (Hash.Trim().Length <= 0)
        {
            return false;
        }

        _videoLocal = RepoFactory.VideoLocal.GetByHash(Hash);
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

    public CommandRequest_AddFileToMyList(ILoggerFactory loggerFactory, IRequestFactory requestFactory,
        ICommandRequestFactory commandFactory, ISettingsProvider settingsProvider) : base(loggerFactory)
    {
        _requestFactory = requestFactory;
        _commandFactory = commandFactory;
        _settingsProvider = settingsProvider;
    }

    protected CommandRequest_AddFileToMyList()
    {
    }
}

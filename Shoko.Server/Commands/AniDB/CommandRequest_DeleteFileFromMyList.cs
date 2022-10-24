using System;
using System.Xml;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Generic;
using Shoko.Server.Providers.AniDB.UDP.User;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using EpisodeType = Shoko.Models.Enums.EpisodeType;
using Void = Shoko.Server.Providers.AniDB.UDP.Generic.Void;

namespace Shoko.Server.Commands.AniDB;

[Serializable]
[Command(CommandRequestType.AniDB_DeleteFileUDP)]
public class CommandRequest_DeleteFileFromMyList : CommandRequestImplementation
{
    private readonly IRequestFactory _requestFactory;

    public string Hash { get; set; }
    public long FileSize { get; set; }
    public EpisodeType EpisodeType { get; set; }
    public int EpisodeNumber { get; set; }
    public int AnimeID { get; set; }
    public int MyListID { get; set; }
    public int FileID { get; set; }

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority10;

    public override QueueStateStruct PrettyDescription => new QueueStateStruct
    {
        message =
            string.IsNullOrEmpty(Hash)
                ? "Deleting file from MyList: {0}, Episode: {1} {2}"
                : "Deleting file from MyList: {0}",
        queueState = QueueStateEnum.AniDB_MyListDelete,
        extraParams = string.IsNullOrEmpty(Hash)
            ? new[] { AnimeID.ToString(), EpisodeType.ToString(), EpisodeNumber.ToString() }
            : new[] { Hash },
    };

    protected override void Process()
    {
        // there will be a road bump the first time we start up, as some people may have requests with MyListID. I don't care. It'll get there.
        Logger.LogInformation("Processing CommandRequest_DeleteFileFromMyList: Hash: {Hash} FileSize: {Size} MyListID: {MyListID} FileID: {FileID} AnimeID: {AnimeID} Episode: {EpisodeType} {EpisodeNumber}",
            Hash, FileSize, MyListID, FileID, AnimeID, EpisodeType, EpisodeNumber);

        try
        {
            UDPRequest<Void> request;
            switch (ServerSettings.Instance.AniDb.MyList_DeleteType)
            {
                case AniDBFileDeleteType.Delete:
                    if (!string.IsNullOrEmpty(Hash))
                    {
                        request = _requestFactory.Create<RequestRemoveFile>(
                            r =>
                            {
                                r.Hash = Hash;
                                r.Size = FileSize;
                            }
                        );
                        Logger.LogInformation("Deleting file from MyList: Hash: {Hash}", Hash);
                        request.Execute();
                    }
                    else if (MyListID != 0)
                    {
                        request = _requestFactory.Create<RequestRemoveMyListID>(
                            r =>
                            {
                                r.MyListID = MyListID;
                            }
                        );
                        Logger.LogInformation(
                            "Deleting File from MyList: MyListID: {MyListID}", MyListID);
                        request.Execute();
                    }
                    else if (FileID != 0)
                    {
                        request = _requestFactory.Create<RequestRemoveFileID>(
                            r =>
                            {
                                r.FileID = FileID;
                            }
                        );
                        Logger.LogInformation(
                            "Deleting File from MyList: FileID: {FileID}", FileID);
                        request.Execute();
                    }
                    else
                    {
                        request = _requestFactory.Create<RequestRemoveEpisode>(
                            r =>
                            {
                                r.AnimeID = AnimeID;
                                r.EpisodeType = (Providers.AniDB.EpisodeType)(int)EpisodeType;
                                r.EpisodeNumber = EpisodeNumber;
                            }
                        );
                        Logger.LogInformation(
                            "Deleting Episode from MyList: AnimeID: {AnimeID} Episode: {EpisodeType} {Number}", AnimeID,
                            EpisodeType, EpisodeNumber);
                        request.Execute();
                    }

                    break;
                case AniDBFileDeleteType.MarkDeleted:
                    if (string.IsNullOrEmpty(Hash))
                    {
                        request = _requestFactory.Create<RequestUpdateEpisode>(
                            r =>
                            {
                                r.AnimeID = AnimeID;
                                r.EpisodeType = (Providers.AniDB.EpisodeType)(int)EpisodeType;
                                r.EpisodeNumber = EpisodeNumber;
                                r.State = MyList_State.Deleted;
                            }
                        );
                        Logger.LogInformation(
                            "Marking Episode as deleted in MyList: AnimeID: {AnimeID} Episode: {EpisodeType} {Number}",
                            AnimeID, EpisodeType, EpisodeNumber);
                        request.Execute();
                    }
                    else
                    {
                        request = _requestFactory.Create<RequestUpdateFile>(
                            r =>
                            {
                                r.Hash = Hash;
                                r.Size = FileSize;
                                r.State = MyList_State.Deleted;
                            }
                        );
                        Logger.LogInformation("Marking file as deleted in MyList: Hash: {Hash}", Hash);
                        var response = request.Execute();
                        if (response.Code == UDPReturnCode.NO_SUCH_FILE) Logger.LogWarning("Update MyList returned NO_SUCH_FILE for {Hash}", Hash);
                    }

                    break;
                case AniDBFileDeleteType.MarkUnknown:
                    if (string.IsNullOrEmpty(Hash))
                    {
                        request = _requestFactory.Create<RequestUpdateEpisode>(
                            r =>
                            {
                                r.AnimeID = AnimeID;
                                r.EpisodeType = (Providers.AniDB.EpisodeType)(int)EpisodeType;
                                r.EpisodeNumber = EpisodeNumber;
                                r.State = MyList_State.Unknown;
                            }
                        );
                        Logger.LogInformation(
                            "Marking Episode as unknown in MyList: AnimeID: {AnimeID} Episode: {EpisodeType} {Number}",
                            AnimeID, EpisodeType, EpisodeNumber);
                        request.Execute();
                    }
                    else
                    {
                        request = _requestFactory.Create<RequestUpdateFile>(
                            r =>
                            {
                                r.Hash = Hash;
                                r.Size = FileSize;
                                r.State = MyList_State.Unknown;
                            }
                        );
                        Logger.LogInformation("Marking file as unknown in MyList: Hash: {Hash}", Hash);
                        var response = request.Execute();
                        if (response.Code == UDPReturnCode.NO_SUCH_FILE) Logger.LogWarning("Update MyList returned NO_SUCH_FILE for {Hash}", Hash);
                    }

                    break;
                case AniDBFileDeleteType.DeleteLocalOnly:
                    Logger.LogInformation(
                        "Keeping physical file and AniDB MyList entry, deleting from local DB: Hash: {Hash}", Hash);
                    break;
                case AniDBFileDeleteType.MarkExternalStorage:
                    if (string.IsNullOrEmpty(Hash))
                    {
                        request = _requestFactory.Create<RequestUpdateEpisode>(
                            r =>
                            {
                                r.AnimeID = AnimeID;
                                r.EpisodeType = (Providers.AniDB.EpisodeType)(int)EpisodeType;
                                r.EpisodeNumber = EpisodeNumber;
                                r.State = MyList_State.Remote;
                            }
                        );
                        Logger.LogInformation(
                            "Marking Episode as remote in MyList: AnimeID: {AnimeID} Episode: {EpisodeType} {Number}",
                            AnimeID, EpisodeType, EpisodeNumber);
                        request.Execute();
                    }
                    else
                    {
                        request = _requestFactory.Create<RequestUpdateFile>(
                            r =>
                            {
                                r.Hash = Hash;
                                r.Size = FileSize;
                                r.State = MyList_State.Remote;
                            }
                        );
                        Logger.LogInformation("Marking file as remote in MyList: Hash: {Hash}", Hash);
                        var response = request.Execute();
                        if (response.Code == UDPReturnCode.NO_SUCH_FILE) Logger.LogWarning("Update MyList returned NO_SUCH_FILE for {Hash}", Hash);
                    }

                    break;
                case AniDBFileDeleteType.MarkDisk:
                    if (string.IsNullOrEmpty(Hash))
                    {
                        request = _requestFactory.Create<RequestUpdateEpisode>(
                            r =>
                            {
                                r.AnimeID = AnimeID;
                                r.EpisodeType = (Providers.AniDB.EpisodeType)(int)EpisodeType;
                                r.EpisodeNumber = EpisodeNumber;
                                r.State = MyList_State.Disk;
                            }
                        );
                        Logger.LogInformation(
                            "Marking Episode as Disk in MyList: AnimeID: {AnimeID} Episode: {EpisodeType} {Number}",
                            AnimeID, EpisodeType, EpisodeNumber);
                        request.Execute();
                    }
                    else
                    {
                        request = _requestFactory.Create<RequestUpdateFile>(
                            r =>
                            {
                                r.Hash = Hash;
                                r.Size = FileSize;
                                r.State = MyList_State.Disk;
                            }
                        );
                        Logger.LogInformation("Marking file as Disk in MyList: Hash: {Hash}", Hash);
                        var response = request.Execute();
                        if (response.Code == UDPReturnCode.NO_SUCH_FILE) Logger.LogWarning("Update MyList returned NO_SUCH_FILE for {Hash}", Hash);
                    }

                    break;
            }
        }
        catch (AniDBBannedException ex)
        {
            Logger.LogError(ex, "Error processing {Type}: Hash: {Hash} - {Ex}", GetType().Name, Hash, ex);
        }
    }

    /// <summary>
    /// This should generate a unique key for a command
    /// It will be used to check whether the command has already been queued before adding it
    /// </summary>
    public override void GenerateCommandID()
    {
        CommandID = $"CommandRequest_DeleteFileFromMyList_{Hash}_{this.FileID}_{MyListID}_{AnimeID}_{EpisodeType}{EpisodeNumber}";
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

            if (int.TryParse(TryGetProperty(docCreator, "CommandRequest_DeleteFileFromMyList", "MyListID"),
                    out var mylistID) && mylistID != 0)
            {
                var vid = RepoFactory.VideoLocal.GetByMyListID(mylistID);
                if (vid != null)
                {
                    Hash = vid.Hash;
                    FileSize = vid.FileSize;
                }
            }

            // populate the fields
            Hash = TryGetProperty(docCreator, "CommandRequest_DeleteFileFromMyList", nameof(Hash));
            FileSize = long.Parse(
                TryGetProperty(docCreator, "CommandRequest_DeleteFileFromMyList", nameof(FileSize))
            );

            if (Enum.TryParse<EpisodeType>(
                    TryGetProperty(docCreator, "CommandRequest_DeleteFileFromMyList", nameof(EpisodeType)),
                    out var episodeType))
                EpisodeType = episodeType;
            if (int.TryParse(
                    TryGetProperty(docCreator, "CommandRequest_DeleteFileFromMyList", nameof(EpisodeNumber)),
                    out var epNum))
                EpisodeNumber = epNum;
            if (int.TryParse(TryGetProperty(docCreator, "CommandRequest_DeleteFileFromMyList", nameof(AnimeID)),
                    out var animeID))
                AnimeID = animeID;
        }

        if (Hash.Trim().Length > 0 || AnimeID > 0)
            return true;

        return false;
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

    public CommandRequest_DeleteFileFromMyList(ILoggerFactory loggerFactory, IRequestFactory requestFactory) : base(
        loggerFactory)
    {
        _requestFactory = requestFactory;
        EpisodeType = EpisodeType.Episode; // default
    }

    protected CommandRequest_DeleteFileFromMyList()
    {
        EpisodeType = EpisodeType.Episode; // default
    }
}

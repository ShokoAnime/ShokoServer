using System;
using System.Xml;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Generic;
using Shoko.Server.Providers.AniDB.UDP.User;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;
using EpisodeType = Shoko.Models.Enums.EpisodeType;
using Void = Shoko.Server.Providers.AniDB.UDP.Generic.Void;

namespace Shoko.Server.Commands.AniDB;

[Serializable]
[Command(CommandRequestType.AniDB_DeleteFileUDP)]
public class CommandRequest_DeleteFileFromMyList : CommandRequestImplementation
{
    private readonly IRequestFactory _requestFactory;
    private readonly ISettingsProvider _settingsProvider;

    public virtual string Hash { get; set; }
    public virtual long FileSize { get; set; }
    public virtual EpisodeType EpisodeType { get; set; }
    public virtual int EpisodeNumber { get; set; }
    public virtual int AnimeID { get; set; }
    public virtual int MyListID { get; set; }
    public virtual int FileID { get; set; }

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

        var settings = _settingsProvider.GetSettings();
        switch (settings.AniDb.MyList_DeleteType)
        {
            case AniDBFileDeleteType.Delete:
                SendDeleteCommand();
                break;
            case AniDBFileDeleteType.MarkDeleted:
                SendUpdateCommand(MyList_State.Deleted);
                break;
            case AniDBFileDeleteType.MarkUnknown:
                SendUpdateCommand(MyList_State.Unknown);
                break;
            case AniDBFileDeleteType.MarkExternalStorage:
                SendUpdateCommand(MyList_State.Remote);
                break;
            case AniDBFileDeleteType.MarkDisk:
                SendUpdateCommand(MyList_State.Disk);
                break;
            case AniDBFileDeleteType.DeleteLocalOnly:
                Logger.LogInformation(
                    "Keeping physical file and AniDB MyList entry, deleting from local DB: Hash: {Hash}", Hash);
                break;
        }
    }

    private void SendDeleteCommand()
    {
        UDPRequest<Void> request;
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
            return;
        }

        if (MyListID != 0)
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
            return;
        }

        if (FileID != 0)
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
            return;
        }

        if (AnimeID == 0) return;

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

    private void SendUpdateCommand(MyList_State state)
    {
        UDPRequest<Void> request;
        if (!string.IsNullOrEmpty(Hash))
        {
            request = _requestFactory.Create<RequestUpdateFile>(
                r =>
                {
                    r.Hash = Hash;
                    r.Size = FileSize;
                    r.State = state;
                }
            );
            Logger.LogInformation("Marking file as {State} in MyList: Hash: {Hash}", state, Hash);
            var response = request.Execute();
            if (response.Code == UDPReturnCode.NO_SUCH_FILE) Logger.LogWarning("Update MyList returned NO_SUCH_FILE for {Hash}", Hash);
            return;
        }

        if (MyListID != 0)
        {
            request = _requestFactory.Create<RequestUpdateMyListID>(
                r =>
                {
                    r.MyListID = MyListID;
                    r.State = state;
                }
            );
            Logger.LogInformation("Marking file as {State} in MyList: MyListID: {MyListID}", state, MyListID);
            var response = request.Execute();
            if (response.Code == UDPReturnCode.NO_SUCH_FILE) Logger.LogWarning("Update MyList returned NO_SUCH_FILE for MyListID: {MyListID}", MyListID);
            return;
        }

        if (FileID != 0)
        {
            request = _requestFactory.Create<RequestUpdateFileID>(
                r =>
                {
                    r.FileID = FileID;
                    r.State = state;
                }
            );
            Logger.LogInformation("Marking file as {State} in MyList: FileID: {FileID}", state, FileID);
            var response = request.Execute();
            if (response.Code == UDPReturnCode.NO_SUCH_FILE) Logger.LogWarning("Update MyList returned NO_SUCH_FILE for FileID: {FileID}", FileID);
            return;
        }

        if (AnimeID == 0) return;

        request = _requestFactory.Create<RequestUpdateEpisode>(
            r =>
            {
                r.AnimeID = AnimeID;
                r.EpisodeType = (Providers.AniDB.EpisodeType)(int)EpisodeType;
                r.EpisodeNumber = EpisodeNumber;
                r.State = state;
            }
        );
        Logger.LogInformation(
            "Marking Episode as {State} in MyList: AnimeID: {AnimeID} Episode: {EpisodeType} {Number}",
            state, AnimeID, EpisodeType, EpisodeNumber);
        request.Execute();
    }

    /// <summary>
    /// This should generate a unique key for a command
    /// It will be used to check whether the command has already been queued before adding it
    /// </summary>
    public override void GenerateCommandID()
    {
        CommandID = $"CommandRequest_DeleteFileFromMyList_{Hash}_{this.FileID}_{MyListID}_{AnimeID}_{EpisodeType}{EpisodeNumber}";
    }

    protected override bool Load()
    {
        // read xml to get parameters
        if (CommandDetails.Trim().Length <= 0) return false;

        var docCreator = new XmlDocument();
        docCreator.LoadXml(CommandDetails);

        if (int.TryParse(docCreator.TryGetProperty("CommandRequest_DeleteFileFromMyList", "MyListID"),
                out var mylistID) && mylistID != 0)
        {
            var vid = RepoFactory.VideoLocal.GetByMyListID(mylistID);
            if (vid == null) return false;

            Hash = vid.Hash;
            FileSize = vid.FileSize;
            return true;
        }

        // populate the fields
        Hash = docCreator.TryGetProperty("CommandRequest_DeleteFileFromMyList", nameof(Hash));
        FileSize = long.Parse(docCreator.TryGetProperty("CommandRequest_DeleteFileFromMyList", nameof(FileSize))
        );

        if (Enum.TryParse<EpisodeType>(docCreator.TryGetProperty("CommandRequest_DeleteFileFromMyList", nameof(EpisodeType)),
                out var episodeType))
            EpisodeType = episodeType;
        if (int.TryParse(docCreator.TryGetProperty("CommandRequest_DeleteFileFromMyList", nameof(EpisodeNumber)),
                out var epNum))
            EpisodeNumber = epNum;
        if (int.TryParse(docCreator.TryGetProperty("CommandRequest_DeleteFileFromMyList", nameof(AnimeID)),
                out var animeID))
            AnimeID = animeID;
            
        if (int.TryParse(docCreator.TryGetProperty("CommandRequest_DeleteFileFromMyList", nameof(FileID)),
                out var fileID))
            FileID = fileID;

        return Hash.Trim().Length > 0 || AnimeID > 0 || FileID > 0;
    }

    public CommandRequest_DeleteFileFromMyList(ILoggerFactory loggerFactory, IRequestFactory requestFactory, ISettingsProvider settingsProvider) : base(
        loggerFactory)
    {
        _requestFactory = requestFactory;
        _settingsProvider = settingsProvider;
        EpisodeType = EpisodeType.Episode; // default
    }

    protected CommandRequest_DeleteFileFromMyList()
    {
        EpisodeType = EpisodeType.Episode; // default
    }
}

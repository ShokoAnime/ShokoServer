using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Generic;
using Shoko.Server.Providers.AniDB.UDP.User;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Settings;
using EpisodeType = Shoko.Models.Enums.EpisodeType;
using Void = Shoko.Server.Providers.AniDB.UDP.Generic.Void;

namespace Shoko.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired]
[AniDBUdpRateLimited]
[DisallowConcurrencyGroup(ConcurrencyGroups.AniDB_UDP)]
[JobKeyGroup(JobKeyGroup.AniDB)]
public class DeleteFileFromMyListJob : BaseJob
{
    private readonly IRequestFactory _requestFactory;
    private readonly ISettingsProvider _settingsProvider;
    
    public string Hash { get; set; }
    public long FileSize { get; set; }
    public EpisodeType EpisodeType { get; set; }
    public int EpisodeNumber { get; set; }
    public int AnimeID { get; set; }
    public int MyListID { get; set; }
    public int FileID { get; set; }

    public override string Name => "Delete File From MyList";
    public override QueueStateStruct Description => new()
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

    public override Task Process()
    {
        // there will be a road bump the first time we start up, as some people may have requests with MyListID. I don't care. It'll get there.
        _logger.LogInformation("Processing CommandRequest_DeleteFileFromMyList: Hash: {Hash} FileSize: {Size} MyListID: {MyListID} FileID: {FileID} AnimeID: {AnimeID} Episode: {EpisodeType} {EpisodeNumber}",
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
                _logger.LogInformation(
                    "Keeping physical file and AniDB MyList entry, deleting from local DB: Hash: {Hash}", Hash);
                break;
        }

        return Task.CompletedTask;
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
            _logger.LogInformation("Deleting file from MyList: Hash: {Hash}", Hash);
            request.Send();
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
            _logger.LogInformation(
                "Deleting File from MyList: MyListID: {MyListID}", MyListID);
            request.Send();
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
            _logger.LogInformation(
                "Deleting File from MyList: FileID: {FileID}", FileID);
            request.Send();
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
        _logger.LogInformation(
            "Deleting Episode from MyList: AnimeID: {AnimeID} Episode: {EpisodeType} {Number}", AnimeID,
            EpisodeType, EpisodeNumber);
        request.Send();
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
            _logger.LogInformation("Marking file as {State} in MyList: Hash: {Hash}", state, Hash);
            var response = request.Send();
            if (response.Code == UDPReturnCode.NO_SUCH_FILE) _logger.LogWarning("Update MyList returned NO_SUCH_FILE for {Hash}", Hash);
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
            _logger.LogInformation("Marking file as {State} in MyList: MyListID: {MyListID}", state, MyListID);
            var response = request.Send();
            if (response.Code == UDPReturnCode.NO_SUCH_FILE) _logger.LogWarning("Update MyList returned NO_SUCH_FILE for MyListID: {MyListID}", MyListID);
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
            _logger.LogInformation("Marking file as {State} in MyList: FileID: {FileID}", state, FileID);
            var response = request.Send();
            if (response.Code == UDPReturnCode.NO_SUCH_FILE) _logger.LogWarning("Update MyList returned NO_SUCH_FILE for FileID: {FileID}", FileID);
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
        _logger.LogInformation(
            "Marking Episode as {State} in MyList: AnimeID: {AnimeID} Episode: {EpisodeType} {Number}",
            state, AnimeID, EpisodeType, EpisodeNumber);
        request.Send();
    }
    
    public DeleteFileFromMyListJob(IRequestFactory requestFactory, ISettingsProvider settingsProvider)
    {
        _requestFactory = requestFactory;
        _settingsProvider = settingsProvider;
    }

    protected DeleteFileFromMyListJob()
    {
    }
}

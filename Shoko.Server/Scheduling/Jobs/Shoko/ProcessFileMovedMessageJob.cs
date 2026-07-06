using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Video.Services;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.Server.Providers.AniDB.Release;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Direct;

namespace Shoko.Server.Scheduling.Jobs.Shoko;

[DatabaseRequired]
[JobKeyGroup(JobKeyGroup.Import)]
public class ProcessFileMovedMessageJob(IVideoReleaseService videoReleaseService, AniDB_MessageRepository anidbMessages, StoredReleaseInfoRepository storedReleaseInfos, VideoLocalRepository videoLocals) : BaseJob
{
    public override string TypeName => "Handle Moved File Message";

    public override string Title => "Handling Moved File Message";

    public int MessageID { get; set; }

    public override async Task Execute()
    {
        _logger.LogInformation("Processing {Job}: {MessageId}", nameof(ProcessFileMovedMessageJob), MessageID);

        var message = anidbMessages.GetByMessageId(MessageID);
        if (message == null) return;

        if (message.IsFileMoveHandled)
        {
            _logger.LogInformation("File moved message already handled: {MessageId}", message.MessageID);
            return;
        }

        // title should be in the format "file moved: <fileID>"
        if (!int.TryParse(message.Title[12..].Trim(), out var fileId))
        {
            throw new Exception("Could not parse file ID from message title");
        }

        var file = storedReleaseInfos.GetByReleaseURI($"{AnidbReleaseProvider.ReleasePrefix}{fileId}");
        if (file == null)
        {
            _logger.LogWarning("Could not find file with AniDB ID: {ID}", fileId);
            return;
        }

        var vlocal = videoLocals.GetByEd2k(file.ED2K);
        if (vlocal == null)
        {
            _logger.LogWarning("Could not find VideoLocal for file with AniDB ID and Hash: {ID} {Hash}", fileId, file.ED2K);
            return;
        }

        // If auto-match is not available then just ignore the file move, since
        // it seems like we don't care about changes like that.
        if (!videoReleaseService.AutoMatchEnabled)
            return;

        await videoReleaseService.ScheduleFindReleaseForVideo(vlocal, force: true).ContinueWith(t =>
        {
            if (!t.IsFaulted)
            {
                // This runs after the file processing job is successfully done, which might be at a later point in time
                // Let us refetch the message to make sure we have the latest data and then mark it as handled
                var msg = anidbMessages.GetByMessageId(MessageID);
                if (msg == null) return;

                msg.IsFileMoveHandled = true;
                anidbMessages.Save(msg);
            }
        });
    }

}

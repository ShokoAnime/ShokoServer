using System.Linq;
using System.Threading.Tasks;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.QueueProcessor.Workers;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling.Jobs.Shoko;

namespace Shoko.Server.Scheduling.Jobs.Actions;

/// <summary>
///   Queues a media info job for every file without media info, or with media
///   info read by an older version than <see cref="VideoLocal.MEDIA_VERSION"/>.
/// </summary>
[DatabaseRequired]
[JobKeyMember("MediaInfoMissing")]
[JobKeyGroup(JobKeyGroup.Actions)]
[DisallowConcurrentExecution]
public class MediaInfoMissingFilesJob(
    IQueueScheduler scheduler,
    VideoLocalRepository videoLocals,
    IJobCancellationAccessor cancellation,
    IJobProgressAccessor progress
) : BaseJob
{
    public override string TypeName => "Schedule MediaInfo Scan for Files Missing It";

    public override string Title => "Scheduling MediaInfo Scan for Files Missing It";

    public override async Task Execute()
    {
        var files = videoLocals.GetAll().Where(video => video.MediaInfo is null || video.MediaVersion < VideoLocal.MEDIA_VERSION).ToList();
        var scheduled = 0;
        progress.Progress.Report(0);
        foreach (var video in files)
        {
            cancellation.Token.ThrowIfCancellationRequested();
            await scheduler.EnqueueWithPriority<MediaInfoJob>(
                job => job.VideoLocalID = video.VideoLocalID,
                JobPriorities.ForFileSize(video.FileSize, isNew: false, prioritize: false)
            );
            progress.Progress.Report(100m * ++scheduled / files.Count);
        }
        progress.Progress.Report(100);
    }
}

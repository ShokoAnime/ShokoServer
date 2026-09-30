using System.Threading.Tasks;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.QueueProcessor.Scheduling;
using Shoko.QueueProcessor.Workers;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling.Jobs.Shoko;

namespace Shoko.Server.Scheduling.Jobs.Actions;

[DatabaseRequired]
[JobKeyMember("MediaInfo")]
[JobKeyGroup(JobKeyGroup.Legacy)]
[DisallowConcurrentExecution]
internal class MediaInfoAllFilesJob(IQueueScheduler scheduler, VideoLocalRepository videoLocals, IJobCancellationAccessor cancellation, IJobProgressAccessor progress) : BaseJob
{
    public override string TypeName => "Schedule MediaInfo Scan for All Files";

    public override string Title => "Scheduling MediaInfo Scan for All Files";

    public override async Task Execute()
    {
        // first build a list of files that we already know about, as we don't want to process them again
        var filesAll = videoLocals.GetAll();
        var scheduled = 0;
        progress.Progress.Report(0);
        foreach (var vl in filesAll)
        {
            cancellation.Token.ThrowIfCancellationRequested();
            await scheduler.StartJob<MediaInfoJob>(c => c.VideoLocalID = vl.VideoLocalID);
            progress.Progress.Report(100m * ++scheduled / filesAll.Count);
        }
        progress.Progress.Report(100);
    }
}

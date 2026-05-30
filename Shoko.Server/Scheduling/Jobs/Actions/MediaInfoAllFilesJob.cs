using System.Threading.Tasks;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.QueueProcessor.Scheduling;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling.Jobs.Shoko;

namespace Shoko.Server.Scheduling.Jobs.Actions;

[DatabaseRequired]
[JobKeyMember("MediaInfo")]
[JobKeyGroup(JobKeyGroup.Legacy)]
[DisallowConcurrentExecution]
internal class MediaInfoAllFilesJob : BaseJob
{
    private readonly IQueueScheduler _scheduler;

    public override string TypeName => "Schedule MediaInfo Scan for All Files";
    public override string Title => "Scheduling MediaInfo Scan for All Files";

    public override async Task Execute()
    {
        // first build a list of files that we already know about, as we don't want to process them again
        var filesAll = _videoLocals.GetAll();
        foreach (var vl in filesAll)
        {
            await _scheduler.StartJob<MediaInfoJob>(c => c.VideoLocalID = vl.VideoLocalID);
        }
    }

    private readonly VideoLocalRepository _videoLocals;
    public MediaInfoAllFilesJob(IQueueScheduler schedulerFactory,
        VideoLocalRepository videoLocals
    )
    {
        _scheduler = schedulerFactory;
        _videoLocals = videoLocals;

    }

    protected MediaInfoAllFilesJob() { }
}

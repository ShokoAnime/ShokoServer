using System.Threading.Tasks;
using Quartz;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Jobs.Shoko;

namespace Shoko.Server.Scheduling.Jobs.Actions;

[DatabaseRequired]
[JobKeyMember("MediaInfo")]
[JobKeyGroup(JobKeyGroup.Legacy)]
[DisallowConcurrentExecution]
internal class MediaInfoAllFilesJob : BaseJob
{
    private readonly ISchedulerFactory _schedulerFactory;

    public override string TypeName => "MediaInfo All Files";
    public override string Title => "Scheduling MediaInfo Scan for All Files";

    public override async Task Process()
    {
        // first build a list of files that we already know about, as we don't want to process them again
        var filesAll = RepoFactory.VideoLocal.GetAll();
        var scheduler = await _schedulerFactory.GetScheduler();
        foreach (var vl in filesAll)
        {
            await scheduler.StartJob<MediaInfoJob>(c => c.VideoLocalID = vl.VideoLocalID);
        }
    }

    public MediaInfoAllFilesJob(ISchedulerFactory schedulerFactory)
    {
        _schedulerFactory = schedulerFactory;
    }

    protected MediaInfoAllFilesJob() { }
}

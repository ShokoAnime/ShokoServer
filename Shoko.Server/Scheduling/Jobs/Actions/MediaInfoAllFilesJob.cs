using System;
using System.Threading.Tasks;
using Quartz;
using QuartzJobFactory.Attributes;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Jobs.Shoko;

namespace Shoko.Server.Scheduling.Jobs.Actions;

[DatabaseRequired]
[JobKeyMember("MediaInfo")]
[JobKeyGroup(JobKeyGroup.Legacy)]
[DisallowConcurrentExecution]
internal class MediaInfoAllFilesJob : IJob
{
    private readonly ISchedulerFactory _schedulerFactory;

    public MediaInfoAllFilesJob(ISchedulerFactory schedulerFactory)
    {
        _schedulerFactory = schedulerFactory;
    }
    
    protected MediaInfoAllFilesJob() { }
    
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            // first build a list of files that we already know about, as we don't want to process them again
            var filesAll = RepoFactory.VideoLocal.GetAll();
            var scheduler = await _schedulerFactory.GetScheduler();
            foreach (var vl in filesAll)
            {
                await scheduler.StartJob<MediaInfoJob>(c => c.VideoLocalID = vl.VideoLocalID);
            }
        }
        catch (Exception ex)
        {
            // TODO: Logging
            // do you want the job to refire?
            throw new JobExecutionException(msg: "", refireImmediately: false, cause: ex);
        }
    }
}

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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
internal class MediaInfoAllFilesJob : IJob
{
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ILogger<MediaInfoAllFilesJob> _logger;

    public MediaInfoAllFilesJob(ISchedulerFactory schedulerFactory, ILogger<MediaInfoAllFilesJob> logger)
    {
        _schedulerFactory = schedulerFactory;
        _logger = logger;
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
            _logger.LogError(ex, "Job threw an error on Execution: {Job} | Error -> {Ex}", context.JobDetail.Key, ex);
            throw new JobExecutionException(msg: "", refireImmediately: false, cause: ex);
        }
    }
}

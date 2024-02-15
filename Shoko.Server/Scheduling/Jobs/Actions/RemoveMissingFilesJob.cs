using System;
using System.Threading.Tasks;
using Quartz;
using QuartzJobFactory.Attributes;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Services;

namespace Shoko.Server.Scheduling.Jobs.Actions;

[DatabaseRequired]
[JobKeyMember("RemoveMissingFiles")]
[JobKeyGroup(JobKeyGroup.Legacy)]
[DisallowConcurrentExecution]
internal class RemoveMissingFilesJob : IJob
{
    private readonly ActionService _actionService;

    [JobKeyMember]
    public bool RemoveMyList { get; set; }

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            await _actionService.RemoveRecordsWithoutPhysicalFiles(RemoveMyList);
        }
        catch (Exception ex)
        {
            // do you want the job to refire?
            throw new JobExecutionException(msg: "", refireImmediately: false, cause: ex);
        }
    }

    public RemoveMissingFilesJob(ActionService actionService)
    {
        _actionService = actionService;
    }

    protected RemoveMissingFilesJob() { }
}

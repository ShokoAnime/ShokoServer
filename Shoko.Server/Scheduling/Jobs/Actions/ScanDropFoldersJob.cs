using System;
using System.Threading.Tasks;
using Quartz;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Services;

namespace Shoko.Server.Scheduling.Jobs.Actions;

[DatabaseRequired]
[JobKeyMember("ScanDropFolders")]
[JobKeyGroup(JobKeyGroup.Legacy)]
[DisallowConcurrentExecution]
internal class ScanDropFoldersJob : IJob
{
    private readonly ActionService _actionService;

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            await _actionService.RunImport_DropFolders();
        }
        catch (Exception ex)
        {
            // do you want the job to refire?
            throw new JobExecutionException(msg: "", refireImmediately: false, cause: ex);
        }
    }

    public ScanDropFoldersJob(ActionService actionService)
    {
        _actionService = actionService;
    }

    protected ScanDropFoldersJob() { }
}

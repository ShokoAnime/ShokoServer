using System;
using System.Threading.Tasks;
using Quartz;
using QuartzJobFactory.Attributes;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Services;

namespace Shoko.Server.Scheduling.Jobs.Shoko;

[DatabaseRequired]
[JobKeyMember("ScanFolder")]
[JobKeyGroup(JobKeyGroup.Actions)]
internal class ScanFolderJob : IJob
{
    private readonly ActionService _actionService;

    [JobKeyMember]
    public int ImportFolderID { get; set; }

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            await _actionService.RunImport_ScanFolder(ImportFolderID);
        }
        catch (Exception ex)
        {
            //logger.Error(ex, ex.ToString());
            // do you want the job to refire?
            throw new JobExecutionException(msg: "", refireImmediately: false, cause: ex);
        }
    }

    public ScanFolderJob(ActionService actionService)
    {
        _actionService = actionService;
    }

    protected ScanFolderJob() { }
}

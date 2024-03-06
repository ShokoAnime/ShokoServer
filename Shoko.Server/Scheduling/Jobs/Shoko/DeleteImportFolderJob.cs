using System;
using System.Threading.Tasks;
using Quartz;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Services;

namespace Shoko.Server.Scheduling.Jobs.Shoko;

[DatabaseRequired]
[JobKeyMember("DeleteImportFolder")]
[JobKeyGroup(JobKeyGroup.Actions)]
internal class DeleteImportFolderJob : IJob
{
    private readonly ActionService _actionService;

    public int ImportFolderID { get; set; }

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            await _actionService.DeleteImportFolder(ImportFolderID);
        }
        catch (Exception ex)
        {
            //logger.Error(ex, ex.ToString());
            // do you want the job to refire?
            throw new JobExecutionException(msg: "", refireImmediately: false, cause: ex);
        }
    }

    public DeleteImportFolderJob(ActionService actionService)
    {
        _actionService = actionService;
    }

    protected DeleteImportFolderJob() { }
}

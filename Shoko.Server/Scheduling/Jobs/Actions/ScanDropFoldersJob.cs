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
internal class ScanDropFoldersJob : BaseJob
{
    private readonly ActionService _actionService;
    public override string TypeName => "Scan Drop Folders";
    public override string Title => "Scanning Drop Folders";

    public override async Task Process()
    {
        await _actionService.RunImport_DetectFiles(onlyInSourceFolders: true);
    }

    public ScanDropFoldersJob(ActionService actionService)
    {
        _actionService = actionService;
    }

    protected ScanDropFoldersJob() { }
}

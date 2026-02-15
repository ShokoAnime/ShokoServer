using System.Threading.Tasks;
using Quartz;
using Shoko.Abstractions.Services;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;

namespace Shoko.Server.Scheduling.Jobs.Actions;

[DatabaseRequired]
[JobKeyMember("ScanDropFolders")]
[JobKeyGroup(JobKeyGroup.Legacy)]
[DisallowConcurrentExecution]
internal class ScanDropFoldersJob : BaseJob
{
    private readonly IVideoService _videoService;

    public override string TypeName => "Scan Drop Folders";
    public override string Title => "Scanning Drop Folders";

    public override async Task Process()
    {
        await _videoService.ScheduleScanForManagedFolders(onlyDropSources: true);
    }

    public ScanDropFoldersJob(IVideoService videoService)
    {
        _videoService = videoService;
    }

    protected ScanDropFoldersJob() { }
}

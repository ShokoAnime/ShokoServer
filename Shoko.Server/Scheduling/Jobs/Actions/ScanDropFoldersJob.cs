using System.Threading.Tasks;
using Shoko.Abstractions.Video.Services;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;

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

    public override async Task Execute()
    {
        await _videoService.ScheduleScanForManagedFolders(onlyDropSources: true);
    }

    public ScanDropFoldersJob(IVideoService videoService)
    {
        _videoService = videoService;
    }

    protected ScanDropFoldersJob() { }
}

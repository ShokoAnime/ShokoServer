using System.Threading.Tasks;
using Shoko.Abstractions.Video.Services;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;

namespace Shoko.Server.Scheduling.Jobs.Actions;

[DatabaseRequired]
[JobKeyMember("ScanDropFolders")]
[JobKeyGroup(JobKeyGroup.Legacy)]
[DisallowConcurrentExecution]
internal class ScanDropFoldersJob(IVideoService videoService) : BaseJob
{
    public override string TypeName => "Scan Drop Folders";

    public override string Title => "Scanning Drop Folders";

    public override async Task Execute()
    {
        await videoService.ScheduleScanForManagedFolders(onlyDropSources: true);
    }
}

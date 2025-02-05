using System.Threading.Tasks;
using Quartz;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Services;

namespace Shoko.Server.Scheduling.Jobs.Actions;

[DatabaseRequired]
[JobKeyMember("Import")]
[JobKeyGroup(JobKeyGroup.Legacy)]
[DisallowConcurrentExecution]
public class ImportJob : BaseJob
{
    private readonly ActionService _service;
    public override string TypeName => "Run Import";
    public override string Title => "Running Import";

    public override async Task Process()
    {
        await _service.RunImport_NewFiles();
        await _service.RunImport_IntegrityCheck();

        // drop folder
        await _service.RunImport_DropFolders();

        // Trakt association checks
        _service.RunImport_ScanTrakt();

        // TMDB association checks
        await _service.RunImport_ScanTMDB();

        // Check for missing images
        await _service.RunImport_GetImages();

        // Check for previously ignored files
        _service.CheckForPreviouslyIgnored();

        await _service.ScheduleMissingAnidbAnimeForFiles();
    }

    public ImportJob(ActionService service)
    {
        _service = service;
    }

    protected ImportJob() { }
}

using System;
using System.Threading.Tasks;
using Quartz;
using QuartzJobFactory.Attributes;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Services;

namespace Shoko.Server.Scheduling.Jobs.Actions;

[DatabaseRequired]
[JobKeyMember("Import")]
[JobKeyGroup(JobKeyGroup.Legacy)]
[DisallowConcurrentExecution]
public class ImportJob : IJob
{
    private readonly ActionService _service;

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            await _service.RunImport_NewFiles();
            await _service.RunImport_IntegrityCheck();

            // drop folder
            await _service.RunImport_DropFolders();

            // TvDB association checks
            await _service.RunImport_ScanTvDB();

            // Trakt association checks
            _service.RunImport_ScanTrakt();

            // MovieDB association checks
            await _service.RunImport_ScanMovieDB();

            // Check for missing images
            await _service.RunImport_GetImages();

            // Check for previously ignored files
            _service.CheckForPreviouslyIgnored();
        }
        catch (Exception ex)
        {
            // do you want the job to refire?
            throw new JobExecutionException(msg: "", refireImmediately: false, cause: ex);
        }
    }

    public ImportJob(ActionService service)
    {
        _service = service;
    }

    protected ImportJob() { }
}

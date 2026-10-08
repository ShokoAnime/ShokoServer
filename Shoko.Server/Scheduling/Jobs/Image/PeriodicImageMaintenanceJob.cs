using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Utilities;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.QueueProcessor.Workers;
using Shoko.Server.Services;
using Shoko.Server.Settings;

#pragma warning disable CS0618
namespace Shoko.Server.Scheduling.Jobs.Image;

[DatabaseRequired]
[DisallowConcurrentExecution]
[JobKeyGroup(JobKeyGroup.Image)]
public class PeriodicImageMaintenanceJob(
    ISettingsProvider settingsProvider,
    ImageManager imageManager,
    IJobCancellationAccessor cancellation,
    IJobProgressAccessor progress
) : BaseJob
{
    public override string TypeName => "Periodic Image Maintenance";

    public override string Title => "Running Periodic Image Maintenance";

    public override async Task Execute()
    {
        var settings = settingsProvider.GetSettings();
        var (purge, validate) = (settings.Image.AutoPurge, settings.Image.AutoValidate);
        var stages = new StagedProgress(progress.Progress, purge ? 1 : 0, validate ? 4 : 0, purge || validate ? 0 : 1);
        stages.Report(0);
        if (purge)
        {
            var days = settings.Metadata.PurgeOrphanedAfterDays;
            _logger.LogInformation("Purging orphaned images older than {Days} days...", days);
            var purged = await imageManager.PurgeOrphanedImages(days, null, stages, cancellation.Token).ConfigureAwait(false);
            _logger.LogInformation("Purged {Count} orphaned images.", purged);
        }

        stages.NextStage();
        cancellation.Token.ThrowIfCancellationRequested();

        if (validate)
        {
            _logger.LogInformation("Validating image integrity...");
            var queued = await imageManager.ValidateAllImages(stages, cancellation.Token).ConfigureAwait(false);
            _logger.LogInformation("Validation queued {Count} images for re-download.", queued);
        }

        stages.Complete();
    }
}

using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Services;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Settings;

#pragma warning disable CS0618
namespace Shoko.Server.Scheduling.Jobs.Image;

[DatabaseRequired]
[DisallowConcurrentExecution]
[JobKeyGroup(JobKeyGroup.Image)]
public class PeriodicImageMaintenanceJob(ISettingsProvider settingsProvider, IImageManager imageManager) : BaseJob
{
    public override string TypeName => "Periodic Image Maintenance";

    public override string Title => "Running Periodic Image Maintenance";

    public override async Task Execute()
    {
        var settings = settingsProvider.GetSettings();
        if (settings.Image.AutoPurge)
        {
            _logger.LogInformation("Purging orphaned images older than 7 days...");
            var purged = await imageManager.PurgeOrphanedImages(daysOld: 7).ConfigureAwait(false);
            _logger.LogInformation("Purged {Count} orphaned images.", purged);
        }

        if (settings.Image.AutoValidate)
        {
            _logger.LogInformation("Validating image integrity...");
            var queued = await imageManager.ValidateAllImages().ConfigureAwait(false);
            _logger.LogInformation("Validation queued {Count} images for re-download.", queued);
        }
    }
}

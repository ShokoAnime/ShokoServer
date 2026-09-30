using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.QueueProcessor.Workers;
using Shoko.Server.Services;

namespace Shoko.Server.Scheduling.Jobs.Image;

[DatabaseRequired]
[LimitConcurrency(1, 1)]
[LongRunning]
[JobKeyGroup(JobKeyGroup.Image)]
public class ValidateAllImagesJob(ImageManager imageManager, IJobCancellationAccessor cancellation, IJobProgressAccessor progress) : BaseJob
{
    public override string TypeName => "Validate All Images";

    public override string Title => "Validating All Images";

    public override async Task Execute()
    {
        _logger.LogInformation("Processing {Job}", nameof(ValidateAllImagesJob));
        var queued = await imageManager.ValidateAllImages(progress.Progress, cancellation.Token).ConfigureAwait(false);
        _logger.LogInformation("Validation finished. Queued {Count} images for forced re-download.", queued);
    }
}

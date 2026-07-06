using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Services;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;

namespace Shoko.Server.Scheduling.Jobs.Image;

[DatabaseRequired]
[LimitConcurrency(1, 1)]
[LongRunning]
[JobKeyGroup(JobKeyGroup.Image)]
public class ValidateAllImagesJob(IImageManager imageManager) : BaseJob
{
    public override string TypeName => "Validate All Images";

    public override string Title => "Validating All Images";

    public override async Task Execute()
    {
        _logger.LogInformation("Processing {Job}", nameof(ValidateAllImagesJob));
        var queued = await imageManager.ValidateAllImages().ConfigureAwait(false);
        _logger.LogInformation("Validation finished. Queued {Count} images for forced re-download.", queued);
    }
}

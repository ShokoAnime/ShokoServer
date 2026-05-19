using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Scheduling.Jobs.Image;

[DatabaseRequired]
[LimitConcurrency(1, 1)]
[JobKeyGroup(JobKeyGroup.Image)]
public class ValidateAllImagesJob : BaseJob
{
    public override string TypeName => "Validate All Images";

    public override string Title => "Validating All Images";

    public override async Task Process()
    {
        _logger.LogInformation("Processing {Job}", nameof(ValidateAllImagesJob));
        var imageManager = Utils.ServiceContainer.GetRequiredService<IImageManager>();
        var queued = await imageManager.ValidateAllImages().ConfigureAwait(false);
        _logger.LogInformation("Validation finished. Queued {Count} images for forced re-download.", queued);
    }

    public ValidateAllImagesJob() { }
}

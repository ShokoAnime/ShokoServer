using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Image.Options;
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
    /// <summary>
    ///   Read each file to check it is a whole image, rather than only
    ///   checking it is there. See <see cref="ImageValidationOptions.ReadContents"/>.
    /// </summary>
    public bool ReadContents { get; set; } = true;

    /// <summary>
    ///   Queue a download of every wanted image found missing or broken. See
    ///   <see cref="ImageValidationOptions.Redownload"/>.
    /// </summary>
    public bool Redownload { get; set; } = true;

    public override string TypeName => "Validate All Images";

    public override string Title => "Validating All Images";

    public override async Task Execute()
    {
        _logger.LogInformation("Processing {Job}", nameof(ValidateAllImagesJob));
        var queued = await imageManager.ValidateAllImages(new() { ReadContents = ReadContents, Redownload = Redownload }, progress.Progress, cancellation.Token).ConfigureAwait(false);
        _logger.LogInformation("Validation finished. Queued {Count} images for forced re-download.", queued);
    }
}

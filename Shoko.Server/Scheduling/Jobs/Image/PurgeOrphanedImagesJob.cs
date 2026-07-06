using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;

namespace Shoko.Server.Scheduling.Jobs.Image;

[DatabaseRequired]
[JobKeyGroup(JobKeyGroup.Image)]
public class PurgeOrphanedImagesJob(IImageManager imageManager) : BaseJob
{
    public int DaysOld { get; set; }

    public DataSource? ImageSource { get; set; }

    public override string TypeName => $"Purge Orphaned Images";

    public override string Title => $"Purging Orphaned Images";

    public override Dictionary<string, object> Details => ImageSource.HasValue
        ? new Dictionary<string, object> { { "Image Source", ImageSource } }
        : [];

    public override async Task Execute()
    {
        _logger.LogInformation("Processing {Job} for {Days} (ImageSource: {ImageSource})", nameof(PurgeOrphanedImagesJob), DaysOld, ImageSource);

        await imageManager.PurgeOrphanedImages(DaysOld, ImageSource);
    }
}

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Services;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;

namespace Shoko.Server.Scheduling.Jobs.Image;

[DatabaseRequired]
[JobKeyGroup(JobKeyGroup.Image)]
// Not default + 50: a prioritized cleanup only moves ahead of other cleanup.
[JobPriority(Default = 0, Prioritized = 10)]
public class PurgeOrphanedImagesJob(IImageManager imageManager) : BaseJob
{
    public int DaysOld { get; set; }

    public MetadataSource? ImageSource { get; set; }

    public override string TypeName => $"Purge Orphaned Images";

    public override string Title => $"Purging Orphaned Images";

    public override Dictionary<string, object> Details => ImageSource is not null
        ? new Dictionary<string, object> { { "Image Source", ImageSource.Name } }
        : [];

    public override async Task Execute()
    {
        _logger.LogInformation("Processing {Job} for {Days} (ImageSource: {ImageSource})", nameof(PurgeOrphanedImagesJob), DaysOld, ImageSource);

        await imageManager.PurgeOrphanedImages(DaysOld, ImageSource);
    }
}

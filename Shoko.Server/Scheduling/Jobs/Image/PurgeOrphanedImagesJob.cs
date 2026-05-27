using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;

#pragma warning disable CS0618
#nullable enable
namespace Shoko.Server.Scheduling.Jobs.Image;

[DatabaseRequired]
[JobKeyGroup(JobKeyGroup.Image)]
public class PurgeOrphanedImagesJob : BaseJob
{
    public PurgeOrphanedImagesJob() { }

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

        var imageManager = ISystemService.StaticServices.GetRequiredService<IImageManager>();
        await imageManager.PurgeOrphanedImages(DaysOld, ImageSource);
    }
}

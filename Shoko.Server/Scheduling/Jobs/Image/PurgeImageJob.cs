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
public class PurgeImageJob(IImageManager imageManager) : BaseJob
{
    public MetadataSource Source { get; set; } = null!;

    public string ResourceID { get; set; } = string.Empty;

    public override string TypeName => $"Purge Image";

    public override string Title => $"Purging Image";

    public override Dictionary<string, object> Details => new()
        {
            { "Source", Source },
            { "Resource ID", ResourceID },
        };

    public override async Task Execute()
    {
        _logger.LogInformation("Processing {Job} for {Source}: {ResourceID}", nameof(PurgeImageJob), Source, ResourceID);

        var image = imageManager.GetImageBySourceAndRemoteResourceID(Source, ResourceID);
        if (image is null)
        {
            _logger.LogWarning("Unable to find image for {Source}: {ResourceID}", Source, ResourceID);
            return;
        }

        await imageManager.PurgeImage(image);
    }
}

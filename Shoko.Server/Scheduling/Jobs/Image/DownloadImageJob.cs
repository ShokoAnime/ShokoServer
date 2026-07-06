using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;

#pragma warning disable CS0618
namespace Shoko.Server.Scheduling.Jobs.Image;

[DatabaseRequired]
[NetworkRequired]
[LimitConcurrency(4)]
[JobKeyGroup(JobKeyGroup.Image)]
public class DownloadImageJob(IImageManager imageManager) : BaseJob
{
    public DataSource Source { get; set; }

    public string ResourceID { get; set; } = string.Empty;

    public bool ForceDownload { get; set; }

    public override string TypeName => $"Download Image";

    public override string Title => $"Downloading Image";

    public override Dictionary<string, object> Details => ForceDownload
        ? new()
        {
            { "Source", Source.ToString() },
            { "Resource ID", ResourceID },
            { "Force Download", true },
        }
        : new()
        {
            { "Source", Source.ToString() },
            { "Resource ID", ResourceID },
        };

    public override async Task Execute()
    {
        _logger.LogInformation("Processing {Job} for {Source}: {ResourceID} (ForceDownload: {ForceDownload})", nameof(DownloadImageJob), Source, ResourceID, ForceDownload);

        var image = imageManager.GetImageBySourceAndRemoteResourceID(Source, ResourceID);
        if (image is null)
        {
            _logger.LogWarning("Unable to find image for {Source}: {ResourceID}", Source, ResourceID);
            return;
        }

        await imageManager.DownloadImage(image, ForceDownload);
    }
}

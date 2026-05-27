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
public class PurgeImageJob : BaseJob
{
    public PurgeImageJob() { }

    public DataSource Source { get; set; }

    public string ResourceID { get; set; } = string.Empty;

    public override string TypeName => $"Purge Image";

    public override string Title => $"Purging Image";

    public override Dictionary<string, object> Details => new()
        {
            { "Source", Source.ToString() },
            { "Resource ID", ResourceID },
        };

    public override async Task Execute()
    {
        _logger.LogInformation("Processing {Job} for {Source}: {ResourceID}", nameof(PurgeImageJob), Source, ResourceID);

        var imageManager = ISystemService.StaticServices.GetRequiredService<IImageManager>();
        var image = imageManager.GetImageBySourceAndRemoteResourceID(Source, ResourceID);
        if (image is null)
        {
            _logger.LogWarning("Unable to find image for {Source}: {ResourceID}", Source, ResourceID);
            return;
        }

        await imageManager.PurgeImage(image);
    }
}

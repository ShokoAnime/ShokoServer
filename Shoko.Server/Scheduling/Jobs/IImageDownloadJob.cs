using Shoko.Abstractions.Metadata.Enums;
using Shoko.QueueProcessor.Abstractions;

#nullable enable
namespace Shoko.Server.Scheduling.Jobs;

public interface IImageDownloadJob : IQueueJob
{
    string? ParentName { get; set; }

    bool ForceDownload { get; set; }

    int ImageID { get; set; }

    ImageEntityType ImageType { get; set; }
}

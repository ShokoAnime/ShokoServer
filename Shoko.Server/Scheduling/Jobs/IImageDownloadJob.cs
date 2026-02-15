using Quartz;
using Shoko.Abstractions.Enums;

#nullable enable
namespace Shoko.Server.Scheduling.Jobs;

public interface IImageDownloadJob : IJob
{
    string? ParentName { get; set; }

    bool ForceDownload { get; set; }

    int ImageID { get; set; }

    ImageEntityType ImageType { get; set; }
}

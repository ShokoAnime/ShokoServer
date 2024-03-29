using Quartz;
using Shoko.Models.Enums;

namespace Shoko.Server.Scheduling.Jobs;

public interface IImageDownloadJob : IJob
{
    string Anime { get; set; }
    int ImageID { get; set; }
    bool ForceDownload { get; set; }
    ImageEntityType ImageType { get; set; }
}

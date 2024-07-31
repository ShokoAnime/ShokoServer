using System.Collections.Generic;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Concurrency;

#nullable enable
namespace Shoko.Server.Scheduling.Jobs.TvDB;

[DatabaseRequired]
[NetworkRequired]
[LimitConcurrency(8, 16)]
[JobKeyGroup(JobKeyGroup.TvDB)]
public class DownloadTvDBImageJob : DownloadImageBaseJob
{
    public override DataSourceEnum Source => DataSourceEnum.TvDB;

    public override Dictionary<string, object> Details => ParentName == null
        ? new()
        {
            { "Type", ImageType.ToString().Replace("_", " ") },
            { "ImageID", ImageID }
        }
        : new()
        {
            { "TvDB Show", ParentName },
            { "Type", ImageType.ToString().Replace("_", " ") },
            { "ImageID", ImageID }
        };

    public DownloadTvDBImageJob() : base() { }

    protected override bool RemoveRecord()
    {
        switch (ImageType)
        {
            case ImageEntityType.Backdrop:
                var fanart = RepoFactory.TvDB_ImageFanart.GetByID(ImageID);
                if (fanart == null)
                    return true;

                RepoFactory.TvDB_ImageFanart.Delete(fanart);
                return true;
            case ImageEntityType.Poster:
                var poster = RepoFactory.TvDB_ImagePoster.GetByID(ImageID);
                if (poster == null)
                    return true;

                RepoFactory.TvDB_ImagePoster.Delete(poster);
                return true;
            case ImageEntityType.Banner:
                var wideBanner = RepoFactory.TvDB_ImageWideBanner.GetByID(ImageID);
                if (wideBanner == null)
                    return true;

                RepoFactory.TvDB_ImageWideBanner.Delete(wideBanner);
                return true;
        }
        return false;
    }
}

using System.Collections.Generic;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Concurrency;

namespace Shoko.Server.Scheduling.Jobs.TMDB;

[DatabaseRequired]
[NetworkRequired]
[LimitConcurrency(10, 16)]
[JobKeyGroup(JobKeyGroup.TMDB)]
public class DownloadTmdbImageJob : DownloadImageBaseJob
{
    public override DataSourceEnum Source => DataSourceEnum.TMDB;

    public override Dictionary<string, object> Details => new()
    {
        { "Type", ImageType.ToString().Replace("_", " ") },
        { "ImageID", ImageID }
    };

    public DownloadTmdbImageJob() : base() { }

    protected override bool RemoveRecord()
    {
        if (RepoFactory.TMDB_Image.GetByID(ImageID) is { } image)
            RepoFactory.TMDB_Image.Delete(image);
        return true;
    }
}

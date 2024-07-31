using System.Collections.Generic;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Concurrency;

namespace Shoko.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired]
[NetworkRequired]
[LimitConcurrency(8, 16)]
[JobKeyGroup(JobKeyGroup.AniDB)]
public class DownloadAniDBImageJob : DownloadImageBaseJob
{
    public override DataSourceEnum Source => DataSourceEnum.AniDB;

    public override Dictionary<string, object> Details => ImageType switch
    {
        ImageEntityType.Poster when ParentName is not null => new()
        {
            { "Anime", ParentName },
            { "Type", "AniDB Poster" },
        },
        ImageEntityType.Poster when ParentName is null => new()
        {
            { "Anime", $"AniDB Anime {ImageID}" },
            { "Type", "AniDB Poster" },
        },
        _ => new()
        {
            { "Anime", ParentName },
            { "Type", $"AniDB {ImageType}".Replace("Person", "Creator") },
            { "ImageID", ImageID }
        }
    };

    public DownloadAniDBImageJob() : base() { }
}

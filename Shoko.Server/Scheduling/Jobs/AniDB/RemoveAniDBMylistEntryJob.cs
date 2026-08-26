using System.Collections.Generic;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Concurrency;

namespace Shoko.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired]
[AniDBUdpRateLimited]
[DisallowConcurrencyGroup(ConcurrencyGroups.AniDB_UDP)]
[JobKeyGroup(JobKeyGroup.AniDB)]
public class RemoveAniDBMylistEntryJob(IMylistService mylistService) : BaseJob
{
    public ulong? MylistID { get; set; }

    public int? FileID { get; set; }

    public string? ED2K { get; set; }
    public long? FileSize { get; set; }

    // generic entries are identified by episode
    public int? AnimeID { get; set; }
    public EpisodeType? EpisodeType { get; set; }
    public int? EpisodeNumber { get; set; }

    public MylistFetchMode FetchMode { get; set; } = MylistFetchMode.Auto;

    public override string TypeName => "Remove Entry from AniDB MyList";

    public override string Title => "Removing Entry from AniDB MyList";

    public override Dictionary<string, object> Details
    {
        get
        {
            var details = new Dictionary<string, object>();
            if (MylistID is > 0)
            {
                details["MyList ID"] = MylistID;
            }
            else if (FileID is > 0)
            {
                details["AniDB File ID"] = FileID;
            }
            else if (AnimeID is > 0)
            {
                details["Anime ID"] = AnimeID;
                details["Episode Type"] = (EpisodeType ?? (EpisodeType)1).ToString();
                details["Episode Number"] = EpisodeNumber ?? 1;
            }
            else if (!string.IsNullOrEmpty(ED2K))
            {
                details["ED2K"] = ED2K;
                if (FileSize > 0)
                    details["FileSize"] = FileSize;
            }

            if (FetchMode is not MylistFetchMode.Auto)
                details["Fetch Mode"] = FetchMode.ToString();

            return details;
        }
    }

    public override async Task Execute()
    {
        if (MylistID is > 0 and { } mylistID)
        {
            await mylistService.RemoveEntryAsync(mylistID, FetchMode);
        }
        else if (FileID is > 0 and { } fileID)
        {
            await mylistService.RemoveEntryAsync(fileID, FetchMode);
        }
        else if (AnimeID is > 0 and { } animeID)
        {
            if (EpisodeType is not { } episodeType || EpisodeNumber is not (> 0 and { } episodeNumber))
                return;

            await mylistService.RemoveEntryAsync(animeID, episodeType, episodeNumber, FetchMode);
        }
        else if (!string.IsNullOrEmpty(ED2K))
        {
            if (FileSize is not (> 0 and { } fileSize))
                return;

            await mylistService.RemoveEntryAsync(ED2K, fileSize, FetchMode);
        }
    }
}

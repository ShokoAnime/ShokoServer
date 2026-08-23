using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Models;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Services;

namespace Shoko.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired]
[AniDBUdpRateLimited]
[DisallowConcurrencyGroup(ConcurrencyGroups.AniDB_UDP)]
[JobKeyGroup(JobKeyGroup.AniDB)]
public class UpdateAniDBMyListEntryJob(IMyListService mylistService, VideoLocalRepository videoRepository) : BaseJob
{
    private VideoLocal? _videoLocal;

    public int? VideoID { get; set; }

    public ulong? MyListID { get; set; }

    public int? FileID { get; set; }

    public string? ED2K { get; set; }

    public long? FileSize { get; set; }

    public int? AnimeID { get; set; }

    public EpisodeType? EpisodeType { get; set; }

    public int? EpisodeNumber { get; set; }

    public bool UpdateSeriesStats { get; set; }

    public MyListFetchMode FetchMode { get; set; } = MyListFetchMode.Auto;

    public MyListState? State { get; set; }

    public MyListFileState? FileState { get; set; }

    public bool? IsViewed { get; set; }

    public DateTime? ViewedAt { get; set; }

    public string? Storage { get; set; }

    public string? Source { get; set; }

    public string? Other { get; set; }

    /// <summary>
    /// The update data to apply to the entry, reconstructed on the fly from
    /// the flattened properties above.
    /// </summary>
    public virtual MyListUpdateData? Data
    {
        get => State is null && FileState is null && IsViewed is null && ViewedAt is null && Storage is null && Source is null && Other is null
            ? null
            : new MyListUpdateData
            {
                State = State,
                FileState = FileState,
                IsViewed = IsViewed,
                ViewedAt = ViewedAt,
                Storage = Storage,
                Source = Source,
                Other = Other,
            };
        set
        {
            State = value?.State;
            FileState = value?.FileState;
            IsViewed = value?.IsViewed;
            ViewedAt = value?.ViewedAt;
            Storage = value?.Storage;
            Source = value?.Source;
            Other = value?.Other;
        }
    }

    public override string TypeName => "Update Entry in AniDB MyList";

    public override string Title => "Updating Entry in AniDB MyList";

    public override Dictionary<string, object> Details
    {
        get
        {
            var details = new Dictionary<string, object>();
            if (VideoID is > 0)
            {
                var filePath = _videoLocal?.FirstValidPlace is { } place ? place.Path ?? place.RelativePath : null;
                if (filePath is { Length: > 0 })
                    details["File Path"] = VideoService.GetDistinctPath(filePath);
                else
                    details["Video"] = VideoID;
            }
            else if (MyListID is > 0)
            {
                details["MyList ID"] = MyListID;
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

            if (State is not null)
                details["Storage State"] = State.Value.ToString();

            if (FileState is not null)
                details["File State"] = FileState.Value.ToString();

            if (IsViewed is not null)
                details["Watched"] = IsViewed;

            if (ViewedAt is not null)
                details["Date"] = ViewedAt;

            if (!string.IsNullOrEmpty(Storage))
                details["Storage Location"] = Storage;

            if (!string.IsNullOrEmpty(Source))
                details["Source"] = Source;

            if (!string.IsNullOrEmpty(Other))
                details["Other"] = Other;

            if (FetchMode is not MyListFetchMode.Auto)
                details["Fetch Mode"] = FetchMode.ToString();

            return details;
        }
    }

    public override void PostInit()
    {
        if (VideoID is > 0)
            _videoLocal = videoRepository.GetByID(VideoID.Value);
    }

    public override async Task Execute()
    {
        var data = Data ?? new MyListUpdateData();

        if (VideoID is > 0 and { } videoID)
        {
            _videoLocal ??= videoRepository.GetByID(videoID);
            if (_videoLocal is null)
                return;

            await mylistService.UpdateVideoAsync(_videoLocal, data, UpdateSeriesStats, FetchMode);
        }
        else if (MyListID is > 0 and { } myListID)
        {
            await mylistService.UpdateEntryAsync(myListID, data, FetchMode);
        }
        else if (FileID is > 0 and { } fileID)
        {
            await mylistService.UpdateEntryAsync(fileID, data, FetchMode);
        }
        else if (AnimeID is > 0 and { } animeID)
        {
            if (EpisodeType is not { } episodeType || EpisodeNumber is not (> 0 and { } episodeNumber))
                return;

            await mylistService.UpdateEntryAsync(animeID, episodeType, episodeNumber, data, FetchMode);
        }
        else if (!string.IsNullOrEmpty(ED2K))
        {
            if (FileSize is not (> 0 and { } fileSize))
                return;

            await mylistService.UpdateEntryAsync(ED2K, fileSize, data, FetchMode);
        }
    }
}

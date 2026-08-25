using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Models;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.Abstractions.Video.Services;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Concurrency;

namespace Shoko.Server.Scheduling.Jobs.AniDB;

/// <summary>
/// Syncs the MyList for a known set of videos. Shares the AniDB HTTP
/// concurrency group with <see cref="SyncAniDBMylistJob"/>, so a scoped sync
/// and a full one never run at the same time and neither has to skip.
/// </summary>
[DatabaseRequired]
[AniDBHttpRateLimited]
[DisallowConcurrencyGroup(ConcurrencyGroups.AniDB_HTTP)]
[DisallowConcurrentExecution]
[LongRunning]
[JobKeyGroup(JobKeyGroup.AniDB)]
public class SyncAniDBMylistVideosJob(IMylistService mylistService, IVideoService videoService) : BaseJob, IJobMerge
{
    /// <summary>
    /// The videos to confine the sync to. An array is not key-eligible on its
    /// own, so <see cref="Key"/> stands in for it.
    /// </summary>
    public int[] VideoIDs { get; set; } = [];

    /// <summary>
    /// Hash key representing the videos to sync, so two syncs over different
    /// sets stay separate jobs while two over the same set collide and merge.
    /// </summary>
    [JobKeyMember]
    public string Key
    {
        get => VideoIDs is { Length: > 0 }
            ? Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(VideoIDs.Order().ToArray()))))
            : string.Empty;
        set { }
    }

    [JobKeyIgnore]
    public MylistFetchMode FetchMode { get; set; } = MylistFetchMode.Auto;

    [JobKeyIgnore]
    public bool ReadWatched { get; set; } = true;

    [JobKeyIgnore]
    public bool ReadUnwatched { get; set; } = true;

    [JobKeyIgnore]
    public bool SetWatched { get; set; } = true;

    [JobKeyIgnore]
    public bool SetUnwatched { get; set; } = true;

    [JobKeyIgnore]
    public MylistWatchedSyncMode WatchedSyncMode { get; set; } = MylistWatchedSyncMode.TrustRemote;

    [JobKeyIgnore]
    public bool UpdateStates { get; set; } = true;

    [JobKeyIgnore]
    public MylistState StorageState { get; set; } = MylistState.HDD;

    [JobKeyIgnore]
    public MylistDeleteType DeleteType { get; set; } = MylistDeleteType.MarkUnknown;

    [JobKeyIgnore]
    public MylistSyncTargets Targets { get; set; } = MylistSyncTargets.All;

    [JobKeyIgnore]
    public MylistWatchedEpisodeMode WatchedEpisodeMode { get; set; } = MylistWatchedEpisodeMode.Ignore;

    /// <summary>
    /// The sync options to run with, reconstructed on the fly from the
    /// flattened properties above.
    /// </summary>
    public virtual MylistSyncOptions Options
    {
        get => new()
        {
            FetchMode = FetchMode,
            ReadWatched = ReadWatched,
            ReadUnwatched = ReadUnwatched,
            SetWatched = SetWatched,
            SetUnwatched = SetUnwatched,
            WatchedSyncMode = WatchedSyncMode,
            UpdateStates = UpdateStates,
            StorageState = StorageState,
            DeleteType = DeleteType,
            Targets = Targets,
            WatchedEpisodeMode = WatchedEpisodeMode,
        };
        set
        {
            FetchMode = value.FetchMode ?? MylistFetchMode.Auto;
            ReadWatched = value.ReadWatched ?? true;
            ReadUnwatched = value.ReadUnwatched ?? true;
            SetWatched = value.SetWatched ?? true;
            SetUnwatched = value.SetUnwatched ?? true;
            WatchedSyncMode = value.WatchedSyncMode ?? MylistWatchedSyncMode.TrustRemote;
            UpdateStates = value.UpdateStates ?? true;
            StorageState = value.StorageState ?? MylistState.HDD;
            DeleteType = value.DeleteType ?? MylistDeleteType.MarkUnknown;
            Targets = value.Targets ?? MylistSyncTargets.All;
            WatchedEpisodeMode = value.WatchedEpisodeMode ?? MylistWatchedEpisodeMode.Ignore;
        }
    }

    public override string TypeName => "Sync AniDB Mylist for Videos";

    public override string Title => "Syncing AniDB Mylist";

    public override Dictionary<string, object> Details
    {
        get
        {
            var details = new Dictionary<string, object> { ["Videos"] = VideoIDs.Length };
            if (FetchMode is not MylistFetchMode.Auto)
                details["Fetch Mode"] = FetchMode.ToString();

            if (ReadWatched)
                details["Read Watched"] = true;

            if (ReadUnwatched)
                details["Read Unwatched"] = true;

            if (SetWatched)
                details["Set Watched"] = true;

            if (SetUnwatched)
                details["Set Unwatched"] = true;

            details["Watched Sync Mode"] = WatchedSyncMode;

            if (UpdateStates)
                details["Update States"] = true;

            details["Storage State"] = StorageState;
            details["Delete Type"] = DeleteType;
            details["Targets"] = Targets;
            if (WatchedEpisodeMode is not MylistWatchedEpisodeMode.Ignore)
                details["Watched Episode Mode"] = WatchedEpisodeMode;

            return details;
        }
    }

    public override async Task Execute()
    {
        var videos = VideoIDs.Select(videoService.GetVideoByID).WhereNotNull()?.ToList() ?? [];
        if (videos.Count is 0)
            return;

        await mylistService.SyncAsync(videos, Options);
    }

    /// <summary>
    /// Absorbs a colliding request rather than dropping it. Two jobs only
    /// collide when they cover the same videos, so what has to be reconciled is
    /// the options, which do not form part of the key.
    /// </summary>
    public bool TryMerge(IQueueJob incoming)
    {
        if (incoming is not SyncAniDBMylistVideosJob other) return false;
        var changed = false;

        // OR-semantics: a flag enabled by either request survives the merge, so
        // the merged sync does at least as much as either asked for
        if (!ReadWatched && other.ReadWatched) { ReadWatched = true; changed = true; }
        if (!ReadUnwatched && other.ReadUnwatched) { ReadUnwatched = true; changed = true; }
        if (!SetWatched && other.SetWatched) { SetWatched = true; changed = true; }
        if (!SetUnwatched && other.SetUnwatched) { SetUnwatched = true; changed = true; }
        if (!UpdateStates && other.UpdateStates) { UpdateStates = true; changed = true; }

        // the fetch mode is a flag set, so the union is the more capable request.
        // Auto is a sentinel rather than a bit pattern, so it cannot be OR-ed
        if (FetchMode is not MylistFetchMode.Auto && other.FetchMode is not MylistFetchMode.Auto && (FetchMode | other.FetchMode) != FetchMode)
        {
            FetchMode |= other.FetchMode;
            changed = true;
        }

        // the targets are a flag set, so the union is the more capable request
        if ((Targets | other.Targets) != Targets) { Targets |= other.Targets; changed = true; }

        // the least destructive delete type wins. Merging is not a request the
        // user made, so it must never escalate one sync's disposal into a
        // harder one than either caller asked for
        if (Rank(other.DeleteType) < Rank(DeleteType)) { DeleteType = other.DeleteType; changed = true; }

        // WatchedSyncMode and StorageState have no "does more work" ordering, so
        // the existing job keeps its own rather than guessing
        return changed;
    }

    /// <summary>
    /// How destructive a delete type is, lowest first.
    /// </summary>
    private static int Rank(MylistDeleteType deleteType)
        => deleteType switch
        {
            MylistDeleteType.DeleteLocalOnly => 0,
            MylistDeleteType.MarkUnknown => 1,
            MylistDeleteType.MarkDisk => 2,
            MylistDeleteType.MarkExternalStorage => 3,
            MylistDeleteType.MarkDeleted => 4,
            MylistDeleteType.Delete => 5,
            _ => 5,
        };
}

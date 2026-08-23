using System.Collections.Generic;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Models;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Concurrency;

namespace Shoko.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired]
[AniDBHttpRateLimited]
[DisallowConcurrencyGroup(ConcurrencyGroups.AniDB_HTTP)]
[DisallowConcurrentExecution]
[LongRunning]
[JobKeyGroup(JobKeyGroup.AniDB)]
public class SyncAniDBMyListJob(IMyListService mylistService) : BaseJob, IJobMerge
{
    [JobKeyIgnore]
    public MyListFetchMode FetchMode { get; set; } = MyListFetchMode.Auto;

    [JobKeyIgnore]
    public bool ReadWatched { get; set; } = true;

    [JobKeyIgnore]
    public bool ReadUnwatched { get; set; } = true;

    [JobKeyIgnore]
    public bool SetWatched { get; set; } = true;

    [JobKeyIgnore]
    public bool SetUnwatched { get; set; } = true;

    [JobKeyIgnore]
    public MyListWatchedSyncMode WatchedSyncMode { get; set; } = MyListWatchedSyncMode.TrustRemote;

    [JobKeyIgnore]
    public bool UpdateStates { get; set; } = true;

    [JobKeyIgnore]
    public MyListState StorageState { get; set; } = MyListState.HDD;

    [JobKeyIgnore]
    public MyListDeleteType DeleteType { get; set; } = MyListDeleteType.MarkUnknown;

    /// <summary>
    /// The sync options to run with, reconstructed on the fly from the
    /// flattened properties above.
    /// </summary>
    public virtual MyListSyncOptions Options
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
        };
        set
        {
            FetchMode = value.FetchMode ?? MyListFetchMode.Auto;
            ReadWatched = value.ReadWatched ?? true;
            ReadUnwatched = value.ReadUnwatched ?? true;
            SetWatched = value.SetWatched ?? true;
            SetUnwatched = value.SetUnwatched ?? true;
            WatchedSyncMode = value.WatchedSyncMode ?? MyListWatchedSyncMode.TrustRemote;
            UpdateStates = value.UpdateStates ?? true;
            StorageState = value.StorageState ?? MyListState.HDD;
            DeleteType = value.DeleteType ?? MyListDeleteType.MarkUnknown;
        }
    }

    public override string TypeName => "Sync AniDB MyList";

    public override string Title => "Syncing AniDB MyList";

    public override Dictionary<string, object> Details
    {
        get
        {
            var details = new Dictionary<string, object>();
            if (FetchMode is not MyListFetchMode.Auto)
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

            return details;
        }
    }

    public override async Task Execute()
    {
        await mylistService.SyncAsync(Options);
    }

    /// <summary>
    /// Absorbs a colliding request rather than dropping it. The options do not
    /// form part of the job key, so two syncs asking for different things
    /// collide by design; without merging, whichever arrived second would be
    /// silently discarded along with whatever extra work it wanted.
    /// </summary>
    public bool TryMerge(IQueueJob incoming)
    {
        if (incoming is not SyncAniDBMyListJob other) return false;
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
        if (FetchMode is not MyListFetchMode.Auto && other.FetchMode is not MyListFetchMode.Auto && (FetchMode | other.FetchMode) != FetchMode)
        {
            FetchMode |= other.FetchMode;
            changed = true;
        }

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
    private static int Rank(MyListDeleteType deleteType)
        => deleteType switch
        {
            MyListDeleteType.DeleteLocalOnly => 0,
            MyListDeleteType.MarkUnknown => 1,
            MyListDeleteType.MarkDisk => 2,
            MyListDeleteType.MarkExternalStorage => 3,
            MyListDeleteType.MarkDeleted => 4,
            MyListDeleteType.Delete => 5,
            _ => 5,
        };
}

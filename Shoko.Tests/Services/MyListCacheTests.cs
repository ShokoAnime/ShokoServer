using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Moq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Models;
using Shoko.Abstractions.Plugin;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Services;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Unit tests for the <see cref="MyListCache"/> desired-state short-circuit
/// helpers, which decide whether an add/update command can be skipped because
/// the cached entry already matches the state that would be sent to AniDB.
/// </summary>
public class MyListCacheTests
{
    private static MyListEntry MakeEntry(
        MyListState state = MyListState.HDD,
        MyListFileState fileState = MyListFileState.Normal,
        bool isViewed = false,
        DateTime? viewedAt = null,
        string? storage = null,
        string? source = null,
        string? other = null
    ) => new()
    {
        MyListID = 1,
        AnimeID = 1,
        EpisodeID = 1,
        FileID = 1,
        State = state,
        FileState = fileState,
        IsViewed = isViewed,
        ViewedAt = viewedAt,
        Storage = storage,
        Source = source,
        Other = other,
    };

    // ── add data ─────────────────────────────────────────────────────────────

    [Fact]
    public void Add_ExactMatch_ReturnsTrue()
    {
        var entry = MakeEntry(state: MyListState.HDD, isViewed: true, viewedAt: new DateTime(2024, 1, 1, 12, 0, 0));
        var data = new MyListAddData
        {
            State = MyListState.HDD,
            IsViewed = true,
            ViewedAt = new DateTime(2024, 1, 1, 12, 0, 0),
        };

        Assert.True(MyListCache.IsInDesiredState(entry, data, null));
    }

    [Fact]
    public void Add_StateMismatch_ReturnsFalse()
    {
        var entry = MakeEntry(state: MyListState.HDD);
        var data = new MyListAddData { State = MyListState.Deleted };

        Assert.False(MyListCache.IsInDesiredState(entry, data, null));
    }

    [Fact]
    public void Add_DefaultStateApplied_WhenDataStateNull()
    {
        var entry = MakeEntry(state: MyListState.Remote);
        var data = new MyListAddData();

        Assert.True(MyListCache.IsInDesiredState(entry, data, null));
    }

    [Fact]
    public void Add_FileStateMismatch_ReturnsFalse()
    {
        var entry = MakeEntry(fileState: MyListFileState.Normal);
        var data = new MyListAddData { FileState = MyListFileState.Corrupted };

        Assert.False(MyListCache.IsInDesiredState(entry, data, null));
    }

    [Fact]
    public void Add_ViewedAtWithinOneSecond_ReturnsTrue()
    {
        var entry = MakeEntry(isViewed: true, viewedAt: new DateTime(2024, 1, 1, 12, 0, 0, 500));
        var data = new MyListAddData
        {
            IsViewed = true,
            ViewedAt = new DateTime(2024, 1, 1, 12, 0, 0),
        };

        Assert.True(MyListCache.IsInDesiredState(entry, data, null));
    }

    [Fact]
    public void Add_ViewedAtFarApart_ReturnsFalse()
    {
        var entry = MakeEntry(isViewed: true, viewedAt: new DateTime(2024, 1, 1, 12, 0, 0));
        var data = new MyListAddData
        {
            IsViewed = true,
            ViewedAt = new DateTime(2024, 1, 1, 13, 0, 0),
        };

        Assert.False(MyListCache.IsInDesiredState(entry, data, null));
    }

    [Fact]
    public void Add_FallbackWatchedDate_UsedWhenDataHasNoViewedState()
    {
        var fallback = new DateTime(2024, 1, 1, 12, 0, 0);
        var entry = MakeEntry(isViewed: true, viewedAt: fallback);
        var data = new MyListAddData();

        Assert.True(MyListCache.IsInDesiredState(entry, data, fallback));
    }

    [Fact]
    public void Add_StorageMismatch_ReturnsFalse()
    {
        var entry = MakeEntry(storage: "HDD1");
        var data = new MyListAddData { Storage = "HDD2" };

        Assert.False(MyListCache.IsInDesiredState(entry, data, null));
    }

    [Fact]
    public void Add_UnsetFields_AreIgnored()
    {
        var entry = MakeEntry(storage: "HDD1", source: "DVD");
        var data = new MyListAddData { State = MyListState.HDD };

        Assert.True(MyListCache.IsInDesiredState(entry, data, null));
    }

    // ── update data ──────────────────────────────────────────────────────────

    [Fact]
    public void Update_ExactMatch_ReturnsTrue()
    {
        var entry = MakeEntry(state: MyListState.HDD, isViewed: true, viewedAt: new DateTime(2024, 1, 1, 12, 0, 0));
        var data = new MyListUpdateData
        {
            State = MyListState.HDD,
            IsViewed = true,
            ViewedAt = new DateTime(2024, 1, 1, 12, 0, 0),
        };

        Assert.True(MyListCache.IsInDesiredState(entry, data));
    }

    [Fact]
    public void Update_StateMismatch_ReturnsFalse()
    {
        var entry = MakeEntry(state: MyListState.HDD);
        var data = new MyListUpdateData { State = MyListState.Deleted };

        Assert.False(MyListCache.IsInDesiredState(entry, data));
    }

    [Fact]
    public void Update_ViewedStateUntouched_WhenDataHasNoViewedFields()
    {
        // update data without viewed fields must not compare viewed state
        var entry = MakeEntry(isViewed: true, viewedAt: new DateTime(2024, 1, 1, 12, 0, 0));
        var data = new MyListUpdateData { State = MyListState.HDD };

        Assert.True(MyListCache.IsInDesiredState(entry, data));
    }

    [Fact]
    public void Update_ViewedMismatch_ReturnsFalse()
    {
        var entry = MakeEntry(isViewed: false);
        var data = new MyListUpdateData { IsViewed = true };

        Assert.False(MyListCache.IsInDesiredState(entry, data));
    }

    [Fact]
    public void Update_FileStateMismatch_ReturnsFalse()
    {
        var entry = MakeEntry(fileState: MyListFileState.Normal);
        var data = new MyListUpdateData { FileState = MyListFileState.Corrupted };

        Assert.False(MyListCache.IsInDesiredState(entry, data));
    }

    [Fact]
    public void Update_StorageMismatch_ReturnsFalse()
    {
        var entry = MakeEntry(storage: "HDD1");
        var data = new MyListUpdateData { Storage = "HDD2" };

        Assert.False(MyListCache.IsInDesiredState(entry, data));
    }

    [Fact]
    public void Add_ViewedStateUntouched_WhenDataHasNoViewedFieldsAndNoFallback()
    {
        // an add without viewed fields sends nothing that would change the viewed
        // state, so an entry watched upstream must still count as in the desired state
        var entry = MakeEntry(isViewed: true, viewedAt: new DateTime(2024, 1, 1, 12, 0, 0));
        var data = new MyListAddData { State = MyListState.HDD };

        Assert.True(MyListCache.IsInDesiredState(entry, data, null));
    }

    [Fact]
    public void Add_TwoArgOverload_BindsToAddDataNotTheImplicitUpdateConversion()
    {
        // MyListAddData implicitly converts to MyListUpdateData, so the two-arg
        // call sites in MyListService must still pick the add overload
        var entry = MakeEntry(isViewed: true, viewedAt: new DateTime(2024, 1, 1, 12, 0, 0));
        var data = new MyListAddData { IsViewed = false };

        // the add overload compares the viewed state; a mismatch must be caught
        Assert.False(MyListCache.IsInDesiredState(entry, data));
    }

    [Fact]
    public void Add_ViewedAtAlone_ImpliesWatched()
    {
        // a watched date on its own means watched, so an unwatched entry is not
        // in the desired state
        var entry = MakeEntry(isViewed: false);
        var data = new MyListAddData { ViewedAt = new DateTime(2024, 1, 1, 12, 0, 0) };

        Assert.False(MyListCache.IsInDesiredState(entry, data));
    }

    [Fact]
    public void Add_ViewedAtAlone_MatchesAWatchedEntry()
    {
        var viewedAt = new DateTime(2024, 1, 1, 12, 0, 0);
        var entry = MakeEntry(isViewed: true, viewedAt: viewedAt);
        var data = new MyListAddData { ViewedAt = viewedAt };

        Assert.True(MyListCache.IsInDesiredState(entry, data));
    }

    [Fact]
    public void Update_ViewedAtAlone_ImpliesWatched()
    {
        var entry = MakeEntry(isViewed: false);
        var data = new MyListUpdateData { ViewedAt = new DateTime(2024, 1, 1, 12, 0, 0) };

        Assert.False(entry.IsViewed);
        Assert.False(MyListCache.IsInDesiredState(entry, data));
    }

    [Fact]
    public void Update_ViewedAtAlone_MatchesAWatchedEntry()
    {
        var viewedAt = new DateTime(2024, 1, 1, 12, 0, 0);
        var entry = MakeEntry(isViewed: true, viewedAt: viewedAt);
        var data = new MyListUpdateData { ViewedAt = viewedAt };

        Assert.True(MyListCache.IsInDesiredState(entry, data));
    }

    // ── unwritable file states ───────────────────────────────────────────────

    [Fact]
    public void OnBluRay_IsNotWritable()
    {
        Assert.False(MyListFileState.OnBluRay.IsWritable);
        Assert.True(MyListFileState.Normal.IsWritable);
        Assert.True(MyListFileState.OnDVD.IsWritable);
        Assert.True(MyListFileState.Other.IsWritable);
    }

    [Fact]
    public void Add_UnwritableFileState_IsIgnored()
    {
        // the request drops it, so treating it as a difference would re-send the
        // same no-op add forever
        var entry = MakeEntry(fileState: MyListFileState.Normal);
        var data = new MyListAddData { FileState = MyListFileState.OnBluRay };

        Assert.True(MyListCache.IsInDesiredState(entry, data));
    }

    [Fact]
    public void Update_UnwritableFileState_IsIgnored()
    {
        var entry = MakeEntry(fileState: MyListFileState.Normal);
        var data = new MyListUpdateData { FileState = MyListFileState.OnBluRay };

        Assert.True(MyListCache.IsInDesiredState(entry, data));
    }

    [Fact]
    public void Update_WritableFileState_IsStillCompared()
    {
        var entry = MakeEntry(fileState: MyListFileState.Normal);
        var data = new MyListUpdateData { FileState = MyListFileState.OnDVD };

        Assert.False(MyListCache.IsInDesiredState(entry, data));
    }

    [Fact]
    public void Add_OptimisticEchoMatchesTheFetchedEntry_AtWirePrecision()
    {
        // AniDB carries whole seconds, so a locally-held sub-second value would
        // never compare equal to what comes back and the entry would look
        // perpetually out of date
        var sent = AniDBExtensions.TruncateToAniDBPrecision(new DateTime(2024, 1, 1, 12, 0, 0, 500, DateTimeKind.Utc))!.Value;
        var fetched = DateTime.UnixEpoch.AddSeconds(AniDBExtensions.GetAniDBDateAsSeconds(sent));

        Assert.Equal(fetched, sent);

        var entry = MakeEntry(isViewed: true, viewedAt: fetched);
        var data = new MyListAddData { IsViewed = true, ViewedAt = sent };
        Assert.True(MyListCache.IsInDesiredState(entry, data));
    }

    [Fact]
    public void EpisodeIndex_IgnoresAnEntryProvenNotGenericByItsHash()
    {
        // an entry identified by ed2k+size has a real file behind it, so it can
        // never be the episode's generic entry — unlike one identified by file
        // ID, which generic entries also have
        var cache = MakeCache(out var dataPath);
        try
        {
            cache.Upsert(new MyListEntry { MyListID = 1, FileID = 100, EpisodeID = 42, ED2K = "abc", Size = 4096, IsGeneric = false });

            Assert.Null(cache.GetByEpisodeID(42));
            Assert.NotNull(cache.GetByEd2k("abc", 4096));
        }
        finally
        {
            Directory.Delete(dataPath, true);
        }
    }

    // ── indexing and eviction ────────────────────────────────────────────────

    private static MyListCache MakeCache(out string dataPath)
    {
        dataPath = Path.Combine(Path.GetTempPath(), "shoko-mylist-cache-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataPath);
        var paths = new Mock<IApplicationPaths>();
        paths.SetupGet(a => a.DataPath).Returns(dataPath);
        return new MyListCache(Mock.Of<ILogger<MyListCache>>(), paths.Object);
    }

    [Fact]
    public void GetAll_IncludesEntriesOnlyKeyedByEd2k()
    {
        var cache = MakeCache(out var dataPath);
        try
        {
            var entry = new MyListEntry { ED2K = "abc", Size = 4096 };
            cache.Upsert(entry);

            Assert.Contains(entry, cache.GetAll());
        }
        finally
        {
            Directory.Delete(dataPath, true);
        }
    }

    [Fact]
    public void Remove_ByEntry_EvictsEntryWithoutAListID()
    {
        var cache = MakeCache(out var dataPath);
        try
        {
            var entry = new MyListEntry { FileID = 678, ED2K = "abc", Size = 4096, EpisodeID = 9876 };
            cache.Upsert(entry);

            cache.Remove(entry);
            cache.Flush();

            Assert.Null(cache.GetByFileID(678));
            Assert.Null(cache.GetByEd2k("abc", 4096));
            Assert.Null(cache.GetByEpisodeID(9876));
            Assert.Empty(cache.GetAll());
        }
        finally
        {
            Directory.Delete(dataPath, true);
        }
    }

    [Fact]
    public void EpisodeIndex_OnlyHoldsGenericEntries()
    {
        var cache = MakeCache(out var dataPath);
        try
        {
            // AniDB reports an episode ID for real files too; only the generic
            // entry stands for the episode itself
            // false means "known not generic", null means the index could not say —
            // neither belongs in the episode index
            var realFile = new MyListEntry { MyListID = 1, FileID = 100, EpisodeID = 42, IsGeneric = false };
            var unknown = new MyListEntry { MyListID = 3, FileID = 300, EpisodeID = 42 };
            cache.Upsert(realFile);
            cache.Upsert(unknown);

            Assert.Null(cache.GetByEpisodeID(42));
            Assert.NotNull(cache.GetByFileID(100));
            Assert.Null(unknown.IsGeneric);

            var generic = new MyListEntry { MyListID = 2, FileID = 200, EpisodeID = 42, IsGeneric = true };
            cache.Upsert(generic);

            Assert.Equal(generic, cache.GetByEpisodeID(42));
        }
        finally
        {
            Directory.Delete(dataPath, true);
        }
    }

    [Fact]
    public void EpisodeIndex_IsNotEvictedByRemovingARealFileForTheSameEpisode()
    {
        var cache = MakeCache(out var dataPath);
        try
        {
            var generic = new MyListEntry { MyListID = 2, FileID = 200, EpisodeID = 42, IsGeneric = true };
            var realFile = new MyListEntry { MyListID = 1, FileID = 100, EpisodeID = 42 };
            cache.Upsert(generic);
            cache.Upsert(realFile);

            cache.Remove(realFile);

            Assert.Equal(generic, cache.GetByEpisodeID(42));
            Assert.Null(cache.GetByFileID(100));
        }
        finally
        {
            Directory.Delete(dataPath, true);
        }
    }

    [Fact]
    public void ReplaceAll_CarriesGenericnessForwardWhenTheFetchCouldNotResolveIt()
    {
        var cache = MakeCache(out var dataPath);
        try
        {
            cache.ReplaceAll([new MyListEntry { MyListID = 2, FileID = 200, EpisodeID = 42, IsGeneric = true }]);
            Assert.NotNull(cache.GetByEpisodeID(42));

            // a later fetch taken while the index was unavailable says nothing
            cache.ReplaceAll([new MyListEntry { MyListID = 2, FileID = 200, EpisodeID = 42 }]);

            Assert.True(cache.GetByLid(2)!.IsGeneric);
            Assert.NotNull(cache.GetByEpisodeID(42));
        }
        finally
        {
            Directory.Delete(dataPath, true);
        }
    }

    [Fact]
    public void ReplaceAll_LetsAFreshAnswerOverrideTheCarriedOne()
    {
        var cache = MakeCache(out var dataPath);
        try
        {
            cache.ReplaceAll([new MyListEntry { MyListID = 2, FileID = 200, EpisodeID = 42, IsGeneric = true }]);
            cache.ReplaceAll([new MyListEntry { MyListID = 2, FileID = 200, EpisodeID = 42, IsGeneric = false }]);

            Assert.False(cache.GetByLid(2)!.IsGeneric);
            Assert.Null(cache.GetByEpisodeID(42));
        }
        finally
        {
            Directory.Delete(dataPath, true);
        }
    }

    [Fact]
    public void Persist_IsDebouncedUntilFlushed()
    {
        var cache = MakeCache(out var dataPath);
        try
        {
            // a sync touches thousands of entries; each one rewriting the whole
            // document under the write lock is what this avoids
            cache.ReplaceAll([new MyListEntry { MyListID = 1, FileID = 100 }]);
            var writtenAt = File.GetLastWriteTimeUtc(Path.Combine(dataPath, "MyList", "mylist.json.gz"));

            cache.Upsert(new MyListEntry { MyListID = 2, FileID = 200 });
            var paths = new Mock<IApplicationPaths>();
            paths.SetupGet(a => a.DataPath).Returns(dataPath);
            Assert.Null(new MyListCache(Mock.Of<ILogger<MyListCache>>(), paths.Object).GetByLid(2));

            cache.Flush();
            Assert.NotEqual(writtenAt, File.GetLastWriteTimeUtc(Path.Combine(dataPath, "MyList", "mylist.json.gz")));
            Assert.NotNull(new MyListCache(Mock.Of<ILogger<MyListCache>>(), paths.Object).GetByLid(2));
        }
        finally
        {
            Directory.Delete(dataPath, true);
        }
    }

    [Fact]
    public void Load_MigratesAnUncompressedCacheAndRemovesIt()
    {
        var dataPath = Path.Combine(Path.GetTempPath(), "shoko-mylist-cache-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dataPath, "MyList"));
        try
        {
            var legacy = Path.Combine(dataPath, "MyList", "mylist.json");
            File.WriteAllText(legacy, "{\"LastFetchedAt\":null,\"Entries\":[{\"MyListID\":7,\"FileID\":70}]}");

            var paths = new Mock<IApplicationPaths>();
            paths.SetupGet(a => a.DataPath).Returns(dataPath);
            var cache = new MyListCache(Mock.Of<ILogger<MyListCache>>(), paths.Object);

            Assert.NotNull(cache.GetByLid(7));

            cache.Flush();
            Assert.True(File.Exists(Path.Combine(dataPath, "MyList", "mylist.json.gz")));
            Assert.False(File.Exists(legacy));

            // and it survives the round trip through the compressed file
            Assert.NotNull(new MyListCache(Mock.Of<ILogger<MyListCache>>(), paths.Object).GetByLid(7));
        }
        finally
        {
            Directory.Delete(dataPath, true);
        }
    }

    [Fact]
    public void Persist_RoundTripsEntriesWithoutAListIDAndTheFetchStamp()
    {
        var cache = MakeCache(out var dataPath);
        try
        {
            cache.ReplaceAll([new MyListEntry { FileID = 678, ED2K = "abc", Size = 4096 }]);
            cache.Flush();
            var fetchedAt = cache.LastFetchedAt;
            Assert.NotNull(fetchedAt);

            var paths = new Mock<IApplicationPaths>();
            paths.SetupGet(a => a.DataPath).Returns(dataPath);
            var reloaded = new MyListCache(Mock.Of<ILogger<MyListCache>>(), paths.Object);

            Assert.NotNull(reloaded.GetByFileID(678));
            Assert.NotNull(reloaded.GetByEd2k("abc", 4096));
            Assert.Equal(fetchedAt, reloaded.LastFetchedAt);
        }
        finally
        {
            Directory.Delete(dataPath, true);
        }
    }
}

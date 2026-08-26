using System;
using System.IO;
using System.IO.Compression;
using Microsoft.Extensions.Logging;
using Moq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Models;
using Shoko.Abstractions.Plugin;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Services.Mylist;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Unit tests for the <see cref="MylistCache"/> desired-state short-circuit
/// helpers, which decide whether an add/update command can be skipped because
/// the cached entry already matches the state that would be sent to AniDB.
/// </summary>
public class MylistCacheTests
{
    private static MylistEntry MakeEntry(
        MylistState state = MylistState.HDD,
        MylistFileState fileState = MylistFileState.Normal,
        bool isViewed = false,
        DateTime? viewedAt = null,
        string? storage = null,
        string? source = null,
        string? other = null
    ) => new()
    {
        MylistID = 1,
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
        var entry = MakeEntry(state: MylistState.HDD, isViewed: true, viewedAt: new DateTime(2024, 1, 1, 12, 0, 0));
        var data = new MylistAddData
        {
            State = MylistState.HDD,
            IsViewed = true,
            ViewedAt = new DateTime(2024, 1, 1, 12, 0, 0),
        };

        Assert.True(MylistCache.IsInDesiredState(entry, data, null));
    }

    [Fact]
    public void Add_StateMismatch_ReturnsFalse()
    {
        var entry = MakeEntry(state: MylistState.HDD);
        var data = new MylistAddData { State = MylistState.Deleted };

        Assert.False(MylistCache.IsInDesiredState(entry, data, null));
    }

    [Fact]
    public void Add_DefaultStateApplied_WhenDataStateNull()
    {
        var entry = MakeEntry(state: MylistState.Remote);
        var data = new MylistAddData();

        Assert.True(MylistCache.IsInDesiredState(entry, data, null));
    }

    [Fact]
    public void Add_FileStateMismatch_ReturnsFalse()
    {
        var entry = MakeEntry(fileState: MylistFileState.Normal);
        var data = new MylistAddData { FileState = MylistFileState.Corrupted };

        Assert.False(MylistCache.IsInDesiredState(entry, data, null));
    }

    [Fact]
    public void Add_ViewedAtWithinOneSecond_ReturnsTrue()
    {
        var entry = MakeEntry(isViewed: true, viewedAt: new DateTime(2024, 1, 1, 12, 0, 0, 500));
        var data = new MylistAddData
        {
            IsViewed = true,
            ViewedAt = new DateTime(2024, 1, 1, 12, 0, 0),
        };

        Assert.True(MylistCache.IsInDesiredState(entry, data, null));
    }

    [Fact]
    public void Add_ViewedAtFarApart_ReturnsFalse()
    {
        var entry = MakeEntry(isViewed: true, viewedAt: new DateTime(2024, 1, 1, 12, 0, 0));
        var data = new MylistAddData
        {
            IsViewed = true,
            ViewedAt = new DateTime(2024, 1, 1, 13, 0, 0),
        };

        Assert.False(MylistCache.IsInDesiredState(entry, data, null));
    }

    [Fact]
    public void Add_FallbackWatchedDate_UsedWhenDataHasNoViewedState()
    {
        var fallback = new DateTime(2024, 1, 1, 12, 0, 0);
        var entry = MakeEntry(isViewed: true, viewedAt: fallback);
        var data = new MylistAddData();

        Assert.True(MylistCache.IsInDesiredState(entry, data, fallback));
    }

    [Fact]
    public void Add_StorageMismatch_ReturnsFalse()
    {
        var entry = MakeEntry(storage: "HDD1");
        var data = new MylistAddData { Storage = "HDD2" };

        Assert.False(MylistCache.IsInDesiredState(entry, data, null));
    }

    [Fact]
    public void Add_UnsetFields_AreIgnored()
    {
        var entry = MakeEntry(storage: "HDD1", source: "DVD");
        var data = new MylistAddData { State = MylistState.HDD };

        Assert.True(MylistCache.IsInDesiredState(entry, data, null));
    }

    // ── update data ──────────────────────────────────────────────────────────

    [Fact]
    public void Update_ExactMatch_ReturnsTrue()
    {
        var entry = MakeEntry(state: MylistState.HDD, isViewed: true, viewedAt: new DateTime(2024, 1, 1, 12, 0, 0));
        var data = new MylistUpdateData
        {
            State = MylistState.HDD,
            IsViewed = true,
            ViewedAt = new DateTime(2024, 1, 1, 12, 0, 0),
        };

        Assert.True(MylistCache.IsInDesiredState(entry, data));
    }

    [Fact]
    public void Update_StateMismatch_ReturnsFalse()
    {
        var entry = MakeEntry(state: MylistState.HDD);
        var data = new MylistUpdateData { State = MylistState.Deleted };

        Assert.False(MylistCache.IsInDesiredState(entry, data));
    }

    [Fact]
    public void Update_ViewedStateUntouched_WhenDataHasNoViewedFields()
    {
        // update data without viewed fields must not compare viewed state
        var entry = MakeEntry(isViewed: true, viewedAt: new DateTime(2024, 1, 1, 12, 0, 0));
        var data = new MylistUpdateData { State = MylistState.HDD };

        Assert.True(MylistCache.IsInDesiredState(entry, data));
    }

    [Fact]
    public void Update_ViewedMismatch_ReturnsFalse()
    {
        var entry = MakeEntry(isViewed: false);
        var data = new MylistUpdateData { IsViewed = true };

        Assert.False(MylistCache.IsInDesiredState(entry, data));
    }

    [Fact]
    public void Update_FileStateMismatch_ReturnsFalse()
    {
        var entry = MakeEntry(fileState: MylistFileState.Normal);
        var data = new MylistUpdateData { FileState = MylistFileState.Corrupted };

        Assert.False(MylistCache.IsInDesiredState(entry, data));
    }

    [Fact]
    public void Update_StorageMismatch_ReturnsFalse()
    {
        var entry = MakeEntry(storage: "HDD1");
        var data = new MylistUpdateData { Storage = "HDD2" };

        Assert.False(MylistCache.IsInDesiredState(entry, data));
    }

    [Fact]
    public void Add_ViewedStateUntouched_WhenDataHasNoViewedFieldsAndNoFallback()
    {
        // an add without viewed fields sends nothing that would change the viewed
        // state, so an entry watched upstream must still count as in the desired state
        var entry = MakeEntry(isViewed: true, viewedAt: new DateTime(2024, 1, 1, 12, 0, 0));
        var data = new MylistAddData { State = MylistState.HDD };

        Assert.True(MylistCache.IsInDesiredState(entry, data, null));
    }

    [Fact]
    public void Add_TwoArgOverload_BindsToAddDataNotTheImplicitUpdateConversion()
    {
        // MylistAddData implicitly converts to MylistUpdateData, so the two-arg
        // call sites in MylistService must still pick the add overload
        var entry = MakeEntry(isViewed: true, viewedAt: new DateTime(2024, 1, 1, 12, 0, 0));
        var data = new MylistAddData { IsViewed = false };

        // the add overload compares the viewed state; a mismatch must be caught
        Assert.False(MylistCache.IsInDesiredState(entry, data));
    }

    [Fact]
    public void Add_ViewedAtAlone_ImpliesWatched()
    {
        // a watched date on its own means watched, so an unwatched entry is not
        // in the desired state
        var entry = MakeEntry(isViewed: false);
        var data = new MylistAddData { ViewedAt = new DateTime(2024, 1, 1, 12, 0, 0) };

        Assert.False(MylistCache.IsInDesiredState(entry, data));
    }

    [Fact]
    public void Add_ViewedAtAlone_MatchesAWatchedEntry()
    {
        var viewedAt = new DateTime(2024, 1, 1, 12, 0, 0);
        var entry = MakeEntry(isViewed: true, viewedAt: viewedAt);
        var data = new MylistAddData { ViewedAt = viewedAt };

        Assert.True(MylistCache.IsInDesiredState(entry, data));
    }

    [Fact]
    public void Update_ViewedAtAlone_ImpliesWatched()
    {
        var entry = MakeEntry(isViewed: false);
        var data = new MylistUpdateData { ViewedAt = new DateTime(2024, 1, 1, 12, 0, 0) };

        Assert.False(entry.IsViewed);
        Assert.False(MylistCache.IsInDesiredState(entry, data));
    }

    [Fact]
    public void Update_ViewedAtAlone_MatchesAWatchedEntry()
    {
        var viewedAt = new DateTime(2024, 1, 1, 12, 0, 0);
        var entry = MakeEntry(isViewed: true, viewedAt: viewedAt);
        var data = new MylistUpdateData { ViewedAt = viewedAt };

        Assert.True(MylistCache.IsInDesiredState(entry, data));
    }

    // ── unwritable file states ───────────────────────────────────────────────

    [Fact]
    public void OnBluRay_IsNotWritable()
    {
        Assert.False(MylistFileState.OnBluRay.IsWritable);
        Assert.True(MylistFileState.Normal.IsWritable);
        Assert.True(MylistFileState.OnDVD.IsWritable);
        Assert.True(MylistFileState.Other.IsWritable);
    }

    [Fact]
    public void Add_UnwritableFileState_IsIgnored()
    {
        // the request drops it, so treating it as a difference would re-send the
        // same no-op add forever
        var entry = MakeEntry(fileState: MylistFileState.Normal);
        var data = new MylistAddData { FileState = MylistFileState.OnBluRay };

        Assert.True(MylistCache.IsInDesiredState(entry, data));
    }

    [Fact]
    public void Update_UnwritableFileState_IsIgnored()
    {
        var entry = MakeEntry(fileState: MylistFileState.Normal);
        var data = new MylistUpdateData { FileState = MylistFileState.OnBluRay };

        Assert.True(MylistCache.IsInDesiredState(entry, data));
    }

    [Fact]
    public void Update_WritableFileState_IsStillCompared()
    {
        var entry = MakeEntry(fileState: MylistFileState.Normal);
        var data = new MylistUpdateData { FileState = MylistFileState.OnDVD };

        Assert.False(MylistCache.IsInDesiredState(entry, data));
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
        var data = new MylistAddData { IsViewed = true, ViewedAt = sent };
        Assert.True(MylistCache.IsInDesiredState(entry, data));
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
            cache.Upsert(new MylistEntry { MylistID = 1, FileID = 100, EpisodeID = 42, ED2K = "abc", Size = 4096, IsGeneric = false });

            Assert.Null(cache.GetByEpisodeID(42));
            Assert.NotNull(cache.GetByEd2k("abc", 4096));
        }
        finally
        {
            Directory.Delete(dataPath, true);
        }
    }

    // ── indexing and eviction ────────────────────────────────────────────────

    private static MylistCache MakeCache(out string dataPath)
    {
        dataPath = Path.Combine(Path.GetTempPath(), "shoko-mylist-cache-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataPath);
        var paths = new Mock<IApplicationPaths>();
        paths.SetupGet(a => a.DataPath).Returns(dataPath);
        return new MylistCache(Mock.Of<ILogger<MylistCache>>(), paths.Object);
    }

    [Fact]
    public void GetAll_IncludesEntriesOnlyKeyedByEd2k()
    {
        var cache = MakeCache(out var dataPath);
        try
        {
            var entry = new MylistEntry { ED2K = "abc", Size = 4096 };
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
            var entry = new MylistEntry { FileID = 678, ED2K = "abc", Size = 4096, EpisodeID = 9876 };
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
            var realFile = new MylistEntry { MylistID = 1, FileID = 100, EpisodeID = 42, IsGeneric = false };
            var unknown = new MylistEntry { MylistID = 3, FileID = 300, EpisodeID = 42 };
            cache.Upsert(realFile);
            cache.Upsert(unknown);

            Assert.Null(cache.GetByEpisodeID(42));
            Assert.NotNull(cache.GetByFileID(100));
            Assert.Null(unknown.IsGeneric);

            var generic = new MylistEntry { MylistID = 2, FileID = 200, EpisodeID = 42, IsGeneric = true };
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
            var generic = new MylistEntry { MylistID = 2, FileID = 200, EpisodeID = 42, IsGeneric = true };
            var realFile = new MylistEntry { MylistID = 1, FileID = 100, EpisodeID = 42 };
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
            cache.ReplaceAll([new MylistEntry { MylistID = 2, FileID = 200, EpisodeID = 42, IsGeneric = true }]);
            Assert.NotNull(cache.GetByEpisodeID(42));

            // a later fetch taken while the index was unavailable says nothing
            cache.ReplaceAll([new MylistEntry { MylistID = 2, FileID = 200, EpisodeID = 42 }]);

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
            cache.ReplaceAll([new MylistEntry { MylistID = 2, FileID = 200, EpisodeID = 42, IsGeneric = true }]);
            cache.ReplaceAll([new MylistEntry { MylistID = 2, FileID = 200, EpisodeID = 42, IsGeneric = false }]);

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
            cache.ReplaceAll([new MylistEntry { MylistID = 1, FileID = 100 }]);
            var writtenAt = File.GetLastWriteTimeUtc(Path.Combine(dataPath, "MyList", "mylist.json.br"));

            cache.Upsert(new MylistEntry { MylistID = 2, FileID = 200 });
            var paths = new Mock<IApplicationPaths>();
            paths.SetupGet(a => a.DataPath).Returns(dataPath);
            Assert.Null(new MylistCache(Mock.Of<ILogger<MylistCache>>(), paths.Object).GetByLid(2));

            cache.Flush();
            Assert.NotEqual(writtenAt, File.GetLastWriteTimeUtc(Path.Combine(dataPath, "MyList", "mylist.json.br")));
            Assert.NotNull(new MylistCache(Mock.Of<ILogger<MylistCache>>(), paths.Object).GetByLid(2));
        }
        finally
        {
            Directory.Delete(dataPath, true);
        }
    }

    /// <summary>
    /// Nothing recorded when the legacy backup was taken except the file's own
    /// timestamp, so that is what names it once it joins the others.
    /// </summary>
    [Fact]
    public void Load_FilesTheLegacyBackupWithTheRestWithoutReadingIt()
    {
        var dataPath = Path.Combine(Path.GetTempPath(), "shoko-mylist-cache-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dataPath, "MyList"));
        try
        {
            // the same bare array the current backups hold, only uncompressed
            // and in a shape whose entries lack fields the sync compares on
            var legacy = Path.Combine(dataPath, "MyList", "mylist.json");
            File.WriteAllText(legacy, "[{\"MylistID\":7,\"FileID\":70}]");
            File.SetLastWriteTimeUtc(legacy, new DateTime(2022, 6, 17, 19, 13, 3, DateTimeKind.Utc));

            var paths = new Mock<IApplicationPaths>();
            paths.SetupGet(a => a.DataPath).Returns(dataPath);
            var cache = new MylistCache(Mock.Of<ILogger<MylistCache>>(), paths.Object);

            Assert.Null(cache.GetByLid(7));
            Assert.False(File.Exists(legacy));

            // an empty cache has never been fetched, so the next sync fetches one
            Assert.Null(cache.LastFetchedAt);

            var archived = Path.Combine(dataPath, "MyList", "Backups", "2022-06-17 19_13_03Z legacy.json.gz");
            Assert.True(File.Exists(archived));

            // and it is a backup like any other, so rotation will age it out
            Assert.Contains(
                new DirectoryInfo(Path.Combine(dataPath, "MyList", "Backups")).GetFiles(MylistBackups.RotationPattern),
                file => file.FullName == archived
            );

            // compressed the way the current backups are, contents carried over as they were
            using var fileStream = File.OpenRead(archived);
            using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
            using var reader = new StreamReader(gzipStream);
            Assert.Equal("[{\"MylistID\":7,\"FileID\":70}]", reader.ReadToEnd());
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
            cache.ReplaceAll([new MylistEntry { FileID = 678, ED2K = "abc", Size = 4096 }]);
            cache.Flush();
            var fetchedAt = cache.LastFetchedAt;
            Assert.NotNull(fetchedAt);

            var paths = new Mock<IApplicationPaths>();
            paths.SetupGet(a => a.DataPath).Returns(dataPath);
            var reloaded = new MylistCache(Mock.Of<ILogger<MylistCache>>(), paths.Object);

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

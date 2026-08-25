using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Xunit;

namespace Shoko.Tests.Scheduling.Jobs.AniDB;

/// <summary>
/// The sync job's options are excluded from its key, so two syncs asking for
/// different things collide. These cover what the merge does with them.
/// </summary>
public class SyncAniDBMylistJobTests
{
    [Fact]
    public void TryMerge_EnablesFlagsEitherRequestAskedFor()
    {
        var existing = new SyncAniDBMylistJob(null!) { ReadWatched = false, SetUnwatched = false };
        var incoming = new SyncAniDBMylistJob(null!) { ReadWatched = true, SetUnwatched = false };

        Assert.True(existing.TryMerge(incoming));
        Assert.True(existing.ReadWatched);
        Assert.False(existing.SetUnwatched);
    }

    [Fact]
    public void TryMerge_UnionsTheFetchMode()
    {
        var existing = new SyncAniDBMylistJob(null!) { FetchMode = MylistFetchMode.Cache };
        var incoming = new SyncAniDBMylistJob(null!) { FetchMode = MylistFetchMode.Cache | MylistFetchMode.IgnoreTimeCheck };

        Assert.True(existing.TryMerge(incoming));
        Assert.True(existing.FetchMode.HasFlag(MylistFetchMode.IgnoreTimeCheck));
    }

    [Fact]
    public void TryMerge_LeavesAutoFetchModeAlone()
    {
        // Auto is a sentinel with every bit set; OR-ing it would swallow the other value
        var existing = new SyncAniDBMylistJob(null!) { FetchMode = MylistFetchMode.Auto };
        var incoming = new SyncAniDBMylistJob(null!) { FetchMode = MylistFetchMode.Http };

        Assert.False(existing.TryMerge(incoming));
        Assert.Equal(MylistFetchMode.Auto, existing.FetchMode);
    }

    [Fact]
    public void TryMerge_KeepsTheLeastDestructiveDeleteType()
    {
        var existing = new SyncAniDBMylistJob(null!) { DeleteType = MylistDeleteType.Delete };
        var incoming = new SyncAniDBMylistJob(null!) { DeleteType = MylistDeleteType.MarkUnknown };

        Assert.True(existing.TryMerge(incoming));
        Assert.Equal(MylistDeleteType.MarkUnknown, existing.DeleteType);
    }

    [Fact]
    public void TryMerge_NeverEscalatesToAHarderDeleteType()
    {
        var existing = new SyncAniDBMylistJob(null!) { DeleteType = MylistDeleteType.DeleteLocalOnly };
        var incoming = new SyncAniDBMylistJob(null!) { DeleteType = MylistDeleteType.Delete };

        Assert.False(existing.TryMerge(incoming));
        Assert.Equal(MylistDeleteType.DeleteLocalOnly, existing.DeleteType);
    }

    [Fact]
    public void TryMerge_ReportsNoChangeWhenAlreadyCovered()
    {
        var existing = new SyncAniDBMylistJob(null!);
        var incoming = new SyncAniDBMylistJob(null!);

        Assert.False(existing.TryMerge(incoming));
    }
}

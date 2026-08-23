using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Xunit;

namespace Shoko.Tests.Scheduling.Jobs.AniDB;

/// <summary>
/// The sync job's options are excluded from its key, so two syncs asking for
/// different things collide. These cover what the merge does with them.
/// </summary>
public class SyncAniDBMyListJobTests
{
    [Fact]
    public void TryMerge_EnablesFlagsEitherRequestAskedFor()
    {
        var existing = new SyncAniDBMyListJob(null!) { ReadWatched = false, SetUnwatched = false };
        var incoming = new SyncAniDBMyListJob(null!) { ReadWatched = true, SetUnwatched = false };

        Assert.True(existing.TryMerge(incoming));
        Assert.True(existing.ReadWatched);
        Assert.False(existing.SetUnwatched);
    }

    [Fact]
    public void TryMerge_UnionsTheFetchMode()
    {
        var existing = new SyncAniDBMyListJob(null!) { FetchMode = MyListFetchMode.Cache };
        var incoming = new SyncAniDBMyListJob(null!) { FetchMode = MyListFetchMode.Cache | MyListFetchMode.IgnoreTimeCheck };

        Assert.True(existing.TryMerge(incoming));
        Assert.True(existing.FetchMode.HasFlag(MyListFetchMode.IgnoreTimeCheck));
    }

    [Fact]
    public void TryMerge_LeavesAutoFetchModeAlone()
    {
        // Auto is a sentinel with every bit set; OR-ing it would swallow the other value
        var existing = new SyncAniDBMyListJob(null!) { FetchMode = MyListFetchMode.Auto };
        var incoming = new SyncAniDBMyListJob(null!) { FetchMode = MyListFetchMode.Http };

        Assert.False(existing.TryMerge(incoming));
        Assert.Equal(MyListFetchMode.Auto, existing.FetchMode);
    }

    [Fact]
    public void TryMerge_KeepsTheLeastDestructiveDeleteType()
    {
        var existing = new SyncAniDBMyListJob(null!) { DeleteType = MyListDeleteType.Delete };
        var incoming = new SyncAniDBMyListJob(null!) { DeleteType = MyListDeleteType.MarkUnknown };

        Assert.True(existing.TryMerge(incoming));
        Assert.Equal(MyListDeleteType.MarkUnknown, existing.DeleteType);
    }

    [Fact]
    public void TryMerge_NeverEscalatesToAHarderDeleteType()
    {
        var existing = new SyncAniDBMyListJob(null!) { DeleteType = MyListDeleteType.DeleteLocalOnly };
        var incoming = new SyncAniDBMyListJob(null!) { DeleteType = MyListDeleteType.Delete };

        Assert.False(existing.TryMerge(incoming));
        Assert.Equal(MyListDeleteType.DeleteLocalOnly, existing.DeleteType);
    }

    [Fact]
    public void TryMerge_ReportsNoChangeWhenAlreadyCovered()
    {
        var existing = new SyncAniDBMyListJob(null!);
        var incoming = new SyncAniDBMyListJob(null!);

        Assert.False(existing.TryMerge(incoming));
    }
}

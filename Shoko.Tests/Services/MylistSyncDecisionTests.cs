using System;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Server.Services.Mylist;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// The watched-state reconciliation rules. A same-day difference is a clash the
/// sync mode breaks; anything older follows the read/set settings, first match
/// winning.
/// </summary>
public class MylistSyncDecisionTests
{
    private static readonly DateTime _watched = new(2026, 3, 14, 20, 30, 0, DateTimeKind.Utc);
    private static readonly DateOnly _sameDay = DateOnly.FromDateTime(_watched);
    private static readonly DateOnly _longAgo = new(2020, 1, 1);

    private static MylistWatchedAction Decide(
        DateTime? local,
        DateTime? remote,
        DateOnly remoteUpdatedAt,
        bool readWatched = false,
        bool readUnwatched = false,
        bool setWatched = false,
        bool setUnwatched = false,
        MylistWatchedSyncMode mode = MylistWatchedSyncMode.TrustRemote
    )
        => MylistSyncDecisions.DecideWatchedAction(local, remote, remoteUpdatedAt, readWatched, readUnwatched, setWatched, setUnwatched, mode);

    #region Older differences

    [Fact]
    public void ReadWatched_ImportsAWatchOnlyAniDBHas()
    {
        var action = Decide(local: null, remote: _watched, _longAgo, readWatched: true);

        Assert.Equal(MylistWatchedActionKind.Import, action.Kind);
        Assert.Equal(_watched, action.Date);
    }

    [Fact]
    public void ReadUnwatched_ImportsAnUnwatch()
    {
        var action = Decide(local: _watched, remote: null, _longAgo, readUnwatched: true);

        Assert.Equal(MylistWatchedActionKind.Import, action.Kind);
        Assert.Null(action.Date);
    }

    [Fact]
    public void SetWatched_ExportsAWatchOnlyTheLibraryHas()
    {
        var action = Decide(local: _watched, remote: null, _longAgo, setWatched: true);

        Assert.Equal(MylistWatchedActionKind.Export, action.Kind);
        Assert.Equal(_watched, action.Date);
    }

    [Fact]
    public void SetUnwatched_ExportsAnUnwatch()
    {
        var action = Decide(local: null, remote: _watched, _longAgo, setUnwatched: true);

        Assert.Equal(MylistWatchedActionKind.Export, action.Kind);
        Assert.Null(action.Date);
    }

    [Fact]
    public void SetWatched_ExportsWhenTheDatesDiffer()
    {
        var action = Decide(local: _watched, remote: _watched.AddDays(-3), _longAgo, setWatched: true);

        Assert.Equal(MylistWatchedActionKind.Export, action.Kind);
        Assert.Equal(_watched, action.Date);
    }

    [Fact]
    public void AgreeingSides_DoNothing()
        => Assert.Equal(MylistWatchedActionKind.None, Decide(_watched, _watched, _longAgo, true, true, true, true).Kind);

    [Fact]
    public void BothUnwatched_DoNothing()
        => Assert.Equal(MylistWatchedActionKind.None, Decide(null, null, _longAgo, true, true, true, true).Kind);

    [Fact]
    public void EverySettingOff_DoesNothing()
        => Assert.Equal(MylistWatchedActionKind.None, Decide(local: null, remote: _watched, _longAgo).Kind);

    [Fact]
    public void ReadWins_WhenReadAndSetBothCouldFire()
    {
        // local unwatched, remote watched: readWatched would import, setUnwatched
        // would export. The read arm is checked first and consumes the branch
        var action = Decide(local: null, remote: _watched, _longAgo, readWatched: true, setUnwatched: true);

        Assert.Equal(MylistWatchedActionKind.Import, action.Kind);
    }

    #endregion

    #region Same-day clashes

    [Fact]
    public void SameDay_TrustLocal_ExportsTheLocalDate()
    {
        var action = Decide(local: _watched, remote: null, _sameDay, readWatched: true, mode: MylistWatchedSyncMode.TrustLocal);

        Assert.Equal(MylistWatchedActionKind.Export, action.Kind);
        Assert.Equal(_watched, action.Date);
    }

    [Fact]
    public void SameDay_TrustRemote_ImportsTheUnwatch()
    {
        var action = Decide(local: _watched, remote: null, _sameDay, setWatched: true, mode: MylistWatchedSyncMode.TrustRemote);

        Assert.Equal(MylistWatchedActionKind.Import, action.Kind);
        Assert.Null(action.Date);
    }

    [Fact]
    public void SameDay_TrustRemote_LeavesAWatchedRemoteAlone()
    {
        // both sides watched, so there is nothing for the remote side to win
        var action = Decide(local: _watched, remote: _watched.AddHours(-2), _sameDay, mode: MylistWatchedSyncMode.TrustRemote);

        Assert.Equal(MylistWatchedActionKind.None, action.Kind);
    }

    [Fact]
    public void SameDay_Ignore_DoesNothingEitherWay()
        => Assert.Equal(MylistWatchedActionKind.None,
            Decide(_watched, null, _sameDay, readWatched: true, readUnwatched: true, setWatched: true, setUnwatched: true, mode: MylistWatchedSyncMode.Ignore).Kind);

    [Fact]
    public void SameDay_TrustLocal_AgreeingSidesDoNothing()
        => Assert.Equal(MylistWatchedActionKind.None, Decide(_watched, _watched, _sameDay, mode: MylistWatchedSyncMode.TrustLocal).Kind);

    [Fact]
    public void SameDay_OverridesTheReadSetSettings()
    {
        // readUnwatched alone would import, but the clash is resolved by the mode
        var action = Decide(local: _watched, remote: null, _sameDay, readUnwatched: true, mode: MylistWatchedSyncMode.TrustLocal);

        Assert.Equal(MylistWatchedActionKind.Export, action.Kind);
    }

    [Fact]
    public void SameDayOnlyAppliesWhenWatchedLocally()
    {
        // with no local date there is no day to clash on, so the read arm applies
        var action = Decide(local: null, remote: _watched, _sameDay, readWatched: true, mode: MylistWatchedSyncMode.Ignore);

        Assert.Equal(MylistWatchedActionKind.Import, action.Kind);
    }

    #endregion
}

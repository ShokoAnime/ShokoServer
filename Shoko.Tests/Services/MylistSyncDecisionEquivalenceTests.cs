using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Server.Services.Mylist;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Pins <see cref="MylistSyncDecisions.DecideWatchedAction"/> to the cascade it
/// was extracted from. <see cref="Original"/> is a transcription of the branch
/// chain that used to live inline in <c>ProcessStates</c> and
/// <c>ProcessGenericEntry</c>, kept here purely as the reference to diff
/// against; if the rules are ever changed on purpose, this test is expected to
/// fail and both sides should be updated together.
/// </summary>
public class MylistSyncDecisionEquivalenceTests
{
    /// <summary>
    /// The pre-extraction branch chain, verbatim in structure. The AniDB-user
    /// guard is left out because it gated the effect rather than the branch, in
    /// the old code and the new alike.
    /// </summary>
    private static (MylistWatchedActionKind Kind, DateTime? Date) Original(
        DateTime? localWatchedDate,
        DateTime? remoteViewedAt,
        DateOnly remoteUpdatedAt,
        bool readWatched,
        bool readUnwatched,
        bool setWatched,
        bool setUnwatched,
        MylistWatchedSyncMode watchedSyncMode
    )
    {
        var updateDate = remoteViewedAt;
        var sameDay = localWatchedDate is not null && remoteUpdatedAt == DateOnly.FromDateTime(localWatchedDate.Value);
        if (sameDay)
        {
            switch (watchedSyncMode)
            {
                case MylistWatchedSyncMode.Ignore:
                    break;

                case MylistWatchedSyncMode.TrustLocal:
                    if (localWatchedDate is not null && !localWatchedDate.Equals(updateDate))
                        return (MylistWatchedActionKind.Export, localWatchedDate.Value.ToUniversalTime());
                    else if (localWatchedDate is null && updateDate is not null)
                        return (MylistWatchedActionKind.Export, null);

                    break;

                case MylistWatchedSyncMode.TrustRemote:
                    if (localWatchedDate is null && updateDate is not null)
                        return (MylistWatchedActionKind.Import, updateDate);
                    else if (localWatchedDate is not null && updateDate is null)
                        return (MylistWatchedActionKind.Import, null);

                    break;
            }

            return (MylistWatchedActionKind.None, null);
        }

        if (readWatched && localWatchedDate == null && updateDate != null)
            return (MylistWatchedActionKind.Import, updateDate);
        if (readUnwatched && localWatchedDate != null && updateDate == null)
            return (MylistWatchedActionKind.Import, null);
        if (setUnwatched && localWatchedDate == null && updateDate != null)
            return (MylistWatchedActionKind.Export, null);
        if (setWatched && localWatchedDate != null && !localWatchedDate.Equals(updateDate))
            return (MylistWatchedActionKind.Export, localWatchedDate.Value.ToUniversalTime());

        return (MylistWatchedActionKind.None, null);
    }

    public static TheoryData<DateTime?, DateTime?, DateOnly, bool, bool, bool, bool, MylistWatchedSyncMode> Grid()
    {
        var anchor = new DateTime(2026, 3, 14, 20, 30, 0, DateTimeKind.Utc);
        var locals = new DateTime?[] { null, anchor, anchor.AddHours(-3), anchor.AddDays(-5), DateTime.SpecifyKind(anchor, DateTimeKind.Unspecified) };
        var remotes = new DateTime?[] { null, anchor, anchor.AddHours(-2), anchor.AddDays(-5) };
        var updatedAts = new[] { DateOnly.FromDateTime(anchor), new DateOnly(2020, 1, 1) };
        var modes = new[] { MylistWatchedSyncMode.Ignore, MylistWatchedSyncMode.TrustLocal, MylistWatchedSyncMode.TrustRemote };

        var data = new TheoryData<DateTime?, DateTime?, DateOnly, bool, bool, bool, bool, MylistWatchedSyncMode>();
        foreach (var local in locals)
            foreach (var remote in remotes)
                foreach (var updatedAt in updatedAts)
                    foreach (var flags in Enumerable_Range16())
                        foreach (var mode in modes)
                            data.Add(local, remote, updatedAt, flags[0], flags[1], flags[2], flags[3], mode);

        return data;

        static IEnumerable<bool[]> Enumerable_Range16()
        {
            for (var i = 0; i < 16; i++)
                yield return [(i & 1) is not 0, (i & 2) is not 0, (i & 4) is not 0, (i & 8) is not 0];
        }
    }

    [Theory]
    [MemberData(nameof(Grid))]
    public void MatchesTheCascadeItReplaced(
        DateTime? local,
        DateTime? remote,
        DateOnly remoteUpdatedAt,
        bool readWatched,
        bool readUnwatched,
        bool setWatched,
        bool setUnwatched,
        MylistWatchedSyncMode mode
    )
    {
        var (expectedKind, expectedDate) = Original(local, remote, remoteUpdatedAt, readWatched, readUnwatched, setWatched, setUnwatched, mode);
        var actual = MylistSyncDecisions.DecideWatchedAction(local, remote, remoteUpdatedAt, readWatched, readUnwatched, setWatched, setUnwatched, mode);

        Assert.Equal(expectedKind, actual.Kind);
        Assert.Equal(expectedDate, actual.Date);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Services;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers <see cref="MetadataCrossReferenceStore"/>'s positioning pass, which
/// decides where an entry's links sit and what gets written. The unique indexes
/// refuse two links in one place, so a reorder has to move whatever is in the
/// way before anything takes its final position.
/// </summary>
public class MetadataLinkOrderingTests
{
    /// <summary>
    /// Records what each write was handed, as it was handed it, since the rows
    /// go on being changed afterwards.
    /// </summary>
    private sealed class Writes
    {
        private readonly List<(int RowID, int Ordering, string ProviderID)[]> _batches = [];

        public IReadOnlyList<(int RowID, int Ordering, string ProviderID)[]> Batches => _batches;

        public void Save(IReadOnlyCollection<CrossRef_AniDB_Metadata_Series> rows)
            => _batches.Add([.. rows.Select(row => (row.CrossRef_AniDB_Metadata_SeriesID, row.Ordering, row.ProviderID))]);

        public IEnumerable<(int RowID, int Ordering, string ProviderID)> Everything
            => _batches.SelectMany(batch => batch);
    }

    private static CrossRef_AniDB_Metadata_Series Link(int rowID, int ordering, string providerID)
        => new() { CrossRef_AniDB_Metadata_SeriesID = rowID, Ordering = ordering, ProviderID = providerID, AnidbAnimeID = 1 };

    [Fact]
    public void ALinkThatStaysWhereItIsIsStillWritten()
    {
        // The contents changed even though the position did not, so skipping
        // the write would drop the change.
        var link = Link(1, 0, "changed");
        var writes = new Writes();

        MetadataCrossReferenceStore.Assign([link], writes.Save);

        Assert.Contains(writes.Everything, row => row.RowID is 1 && row.ProviderID is "changed");
        Assert.Equal(0, link.Ordering);
    }

    [Fact]
    public void PositionsAreHandedOutFromZeroInTheOrderGiven()
    {
        var first = Link(1, 7, "a");
        var second = Link(2, 9, "b");
        var third = Link(3, 4, "c");

        MetadataCrossReferenceStore.Assign([first, second, third], new Writes().Save);

        Assert.Equal(0, first.Ordering);
        Assert.Equal(1, second.Ordering);
        Assert.Equal(2, third.Ordering);
    }

    [Fact]
    public void SwappedLinksAreParkedBeforeEitherTakesTheOthersPlace()
    {
        // Writing 'second' straight into position 0 would collide with 'first',
        // which is still sitting there.
        var first = Link(1, 0, "a");
        var second = Link(2, 1, "b");
        var writes = new Writes();

        MetadataCrossReferenceStore.Assign([second, first], writes.Save);

        Assert.True(writes.Batches.Count >= 2, "the movers should be parked in a write of their own");
        Assert.All(writes.Batches[0], row => Assert.True(row.Ordering < 0, $"row {row.RowID} was not parked, it sat at {row.Ordering}"));
        Assert.Equal(0, second.Ordering);
        Assert.Equal(1, first.Ordering);
    }

    [Fact]
    public void AnUnsavedLinkIsNeverParked()
    {
        // It has no position to collide with, and parking it would write a row
        // that does not exist yet.
        var stored = Link(1, 0, "a");
        var fresh = Link(0, 0, "b");
        var writes = new Writes();

        MetadataCrossReferenceStore.Assign([fresh, stored], writes.Save);

        Assert.DoesNotContain(writes.Batches[0], row => row.RowID is 0);
        Assert.Equal(0, fresh.Ordering);
        Assert.Equal(1, stored.Ordering);
    }
}

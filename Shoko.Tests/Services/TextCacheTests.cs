using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Repositories.Cached.Metadata.Text;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers <see cref="TextCache"/> on its own: how it keys and sorts an entry's
/// rows, how it packs them, that readers never see a write half done, and the
/// overview values it keeps in memory.
/// </summary>
public class TextCacheTests
{
    #region Helpers

    private static int _nextID;

    private static MetadataGuid Entry(string id, MetadataEntityType? entityType = null)
        => new(TestSources.Plugin, entityType ?? MetadataEntityType.Series, id);

    private static MetadataTextRow Row(MetadataGuid entry, MetadataSource source, TextKind kind, string value, int ordering = 0, int? id = null)
    {
        var row = MetadataTextRow.Create(kind);
        row.RowID = id ?? Interlocked.Increment(ref _nextID);
        row.EntryID = entry;
        row.Source = source;
        row.Language = TitleLanguage.English;
        row.LanguageCode = "en";
        row.Value = value;
        row.Ordering = ordering;
        if (kind is TextKind.Title)
            row.TitleType = TitleType.Official;
        return row;
    }

    #endregion

    #region Rows

    [Fact]
    public void ARowTakesTwentyFourBytesAndKeepsItsFlags()
    {
        var row = new TextRow(7, TextKind.Title, 3, 9, TitleType.KanjiReading, false, TextPreference.Overall, 4, 11, "Value");

        Assert.Equal(24, Unsafe.SizeOf<TextRow>());
        Assert.Equal((TextKind.Title, TitleType.KanjiReading, false, TextPreference.Overall), (row.Kind, row.TitleType, row.IsEnabled, row.Preference));

        var overview = new TextRow(8, TextKind.Overview, 0, 0, TitleType.None, true, TextPreference.Language, 0, 0, null);
        Assert.Equal((TextKind.Overview, true, TextPreference.Language, TitleType.None), (overview.Kind, overview.IsEnabled, overview.Preference, overview.TitleType));
    }

    [Fact]
    public void AnEntrysRowsComeBackTitlesFirstThenBySourceThenInOrder()
    {
        var cache = new TextCache();
        var entry = Entry("1");

        cache.Apply(
            [
                Row(entry, TestSources.Plugin, TextKind.Overview, "Overview"),
                Row(entry, TestSources.Plugin, TextKind.Title, "Plugin second", 1),
                Row(entry, TestSources.Plugin, TextKind.Title, "Plugin first", 0),
                Row(entry, MetadataSource.AniDB, TextKind.Title, "AniDB"),
            ],
            []
        );

        Assert.Equal(["AniDB", "Plugin first", "Plugin second"], cache.GetTitles(entry).Select(title => title.Value));
        Assert.Equal(["Plugin first", "Plugin second"], cache.GetTitles(entry, TestSources.Plugin).Select(title => title.Value));
        Assert.Empty(cache.GetTitles(entry, MetadataSource.TMDB));
        Assert.Equal("Overview", Assert.Single(cache.GetOverviews(entry)).Value);
        Assert.Equal(4, cache.GetRows(entry).Length);
    }

    [Theory]
    [InlineData("12")]
    [InlineData("0012")]
    [InlineData("abc")]
    [InlineData("99999999999999999999")]
    [InlineData("0")]
    public void AnEntryIsFoundByItsIDWhateverShapeTheIDHas(string id)
    {
        var cache = new TextCache();
        var entry = Entry(id);
        cache.Apply([Row(entry, TestSources.Plugin, TextKind.Title, id)], []);

        Assert.Equal(id, Assert.Single(cache.GetTitles(entry)).Value);
        Assert.Equal(entry, Assert.Single(cache.Enumerate()).Entity);
        Assert.Empty(cache.GetTitles(Entry(id, MetadataEntityType.Episode)));
    }

    [Fact]
    public void ManyEntriesGrowTheTableAndStayFindable()
    {
        var cache = new TextCache();
        var rows = Enumerable.Range(1, 20_000).Select(index => Row(Entry(index.ToString()), TestSources.Plugin, TextKind.Title, $"Title {index}")).ToList();

        foreach (var chunk in rows.Chunk(1_000))
            cache.Apply(chunk, []);

        Assert.Equal(20_000, cache.EntryCount);
        Assert.All(new[] { 1, 777, 20_000 }, index => Assert.Equal($"Title {index}", Assert.Single(cache.GetTitles(Entry(index.ToString()))).Value));
    }

    [Fact]
    public void AnEntryThatLosesEveryTextIsGone()
    {
        var cache = new TextCache();
        var numeric = Row(Entry("5"), TestSources.Plugin, TextKind.Title, "Numeric");
        var named = Row(Entry("five"), TestSources.Plugin, TextKind.Title, "Named");
        cache.Apply([numeric, named], []);

        cache.Apply([], [numeric, named]);

        Assert.Empty(cache.GetRows(Entry("5")));
        Assert.Empty(cache.GetRows(Entry("five")));
        Assert.Empty(cache.Enumerate());
    }

    [Fact]
    public void APickIsIndexedByTheTextItCopiesAndForgottenWithIt()
    {
        var cache = new TextCache();
        var picked = Row(Entry("1"), TestSources.Plugin, TextKind.Title, "Theirs");
        var pick = Row(Entry("2"), MetadataSource.User, TextKind.Title, "Theirs");
        pick.ReferenceID = picked.RowID;
        cache.Apply([picked, pick], []);

        Assert.Equal([(Entry("2"), pick.RowID)], cache.PicksOf(TextKind.Title, picked.RowID));
        Assert.Empty(cache.PicksOf(TextKind.Overview, picked.RowID));

        cache.Apply([], [pick]);

        Assert.Empty(cache.PicksOf(TextKind.Title, picked.RowID));
    }

    [Fact]
    public void AWriteReportsTheKindsAndSourcesItChangedPerEntry()
    {
        var cache = new TextCache();
        var title = Row(Entry("1"), TestSources.Plugin, TextKind.Title, "A");
        var changes = cache.Apply([title, Row(Entry("2"), MetadataSource.User, TextKind.Overview, "B")], []);

        Assert.Equal(2, changes.Count);
        var first = changes.Single(change => change.EntityID == Entry("1"));
        Assert.Equal([TextKind.Title], first.Kinds);
        Assert.Equal([TestSources.Plugin], first.Sources);
        Assert.Empty(cache.Apply([], []));
    }

    [Fact]
    public void FindingATextByItsIDLooksThroughEveryEntry()
    {
        var cache = new TextCache();
        var title = Row(Entry("1"), TestSources.Plugin, TextKind.Title, "A");
        var overview = Row(Entry("2"), TestSources.Plugin, TextKind.Overview, "B", id: title.RowID);
        cache.Apply([title, overview], []);

        Assert.Equal(Entry("1"), cache.Find(TextKind.Title, title.RowID)?.Entity);
        Assert.Equal(Entry("2"), cache.Find(TextKind.Overview, title.RowID)?.Entity);
        Assert.Null(cache.Find(TextKind.Title, -1));
    }

    #endregion

    #region Concurrency

    [Fact]
    public async Task ReadersNeverSeeAWriteHalfDone()
    {
        const int width = 8;
        var cache = new TextCache();
        var entries = Enumerable.Range(1, 64).Select(index => Entry(index.ToString())).ToList();
        foreach (var entry in entries)
            cache.Apply([.. Enumerable.Range(0, width).Select(position => Row(entry, TestSources.Plugin, TextKind.Title, "0", position))], []);

        using var stop = new CancellationTokenSource();
        var failures = 0;
        var readers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                foreach (var entry in entries)
                {
                    var titles = cache.GetTitles(entry);
                    if (titles.Count != width || titles.Select(title => title.Value).Distinct().Count() is not 1)
                        Interlocked.Increment(ref failures);
                }
            }
        })).ToList();

        // Every write replaces all of an entry's titles with one new value,
        // and new entries keep growing the table under the readers.
        var generation = 0;
        var extra = 1_000;
        for (var round = 1; round <= 200; round++)
        {
            generation++;
            foreach (var entry in entries)
            {
                var current = cache.GetRows(entry);
                cache.Apply(
                    [
                        .. current.Select(row =>
                        {
                            var copy = cache.ToRow(entry, row);
                            copy.Value = generation.ToString();
                            return copy;
                        }),
                    ],
                    []
                );
            }

            cache.Apply([.. Enumerable.Range(0, 50).Select(_ => Row(Entry((extra++).ToString()), TestSources.Plugin, TextKind.Title, "grow"))], []);
        }

        stop.Cancel();
        await Task.WhenAll(readers);
        Assert.Equal(0, failures);
        Assert.All(entries, entry => Assert.All(cache.GetTitles(entry), title => Assert.Equal("200", title.Value)));
    }

    #endregion

    #region Overview Values

    [Fact]
    public void TheOverviewValuesKeptAreBoundedAndTheLeastRecentlyUsedGoFirst()
    {
        var bodies = new OverviewBodyCache(10);
        bodies.Put(1, "aaaa");
        bodies.Put(2, "bbbb");
        Assert.True(bodies.TryGet(1, out _));

        bodies.Put(3, "cccc");

        Assert.True(bodies.TryGet(1, out var first));
        Assert.Equal("aaaa", first);
        Assert.False(bodies.TryGet(2, out _));
        Assert.True(bodies.TryGet(3, out _));
        Assert.Equal(8, bodies.Size);

        bodies.Put(4, new string('d', 50));

        Assert.Equal(1, bodies.Count);
        Assert.True(bodies.TryGet(4, out _));
    }

    [Fact]
    public void AValueReadBeforeAWriteNeverReplacesTheWrittenOne()
    {
        var bodies = new OverviewBodyCache(null);

        // The write lands while the read is running, and the read comes back
        // with the old value.
        bodies.Put(1, "new");

        Assert.Equal("new", bodies.GetOrAdd(1, "old"));
        Assert.True(bodies.TryGet(1, out var kept));
        Assert.Equal("new", kept);
        Assert.Equal("read", bodies.GetOrAdd(2, "read"));
    }

    [Fact]
    public void ACacheWithoutADatabaseKeepsEveryOverviewValue()
    {
        var cache = new TextCache();
        var value = new string('x', 1_000_000);
        var count = (int)(TextCache.DefaultOverviewCapacity / value.Length) + 1;
        var rows = Enumerable.Range(1, count).Select(index => Row(Entry("1"), TestSources.Plugin, TextKind.Overview, value, index)).ToList();

        cache.Apply(rows, []);

        Assert.Equal(count, cache.GetOverviews(Entry("1")).Count(overview => overview.Value.Length == value.Length));
    }

    #endregion
}

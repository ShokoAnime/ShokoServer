using System;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Services;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers <see cref="TextMemoTable"/>, which keeps what the text manager worked out and forgets it
/// together with everything worked out from it.
/// </summary>
public class TextMemoTableTests
{
    private static readonly MetadataGuid Series = new(MetadataSource.Shoko, MetadataEntityType.Series, "1");

    private static readonly MetadataGuid Group = new(MetadataSource.Shoko, MetadataEntityType.Collection, "2");

    private static readonly MetadataGuid Anime = new(MetadataSource.AniDB, MetadataEntityType.Series, "3");

    private static readonly MetadataGuid Show = new(MetadataSource.TMDB, MetadataEntityType.Series, "4");

    [Fact]
    public void AValueIsWorkedOutOnceUntilWhatItReadIsForgotten()
    {
        var table = new TextMemoTable();
        var calls = 0;
        string Title() => table.Get(Series, TextMemoSlot.PreferredTitle, 0, () =>
        {
            calls++;
            table.Record(Anime);
            return "title";
        });

        Title();
        Title();
        Assert.Equal(1, calls);

        table.Forget(Show);
        Title();
        Assert.Equal(1, calls);

        table.Forget(Anime);
        Title();
        Assert.Equal(2, calls);
    }

    [Fact]
    public void ForgettingAnEntryForgetsEveryValueWorkedOutFromItDownTheChain()
    {
        var table = new TextMemoTable();
        var groupCalls = 0;
        string SeriesTitle() => table.Get(Series, TextMemoSlot.PreferredTitle, 0, () =>
        {
            table.Record(Show);
            return "series";
        });
        string GroupName() => table.Get(Group, TextMemoSlot.Name, 0, () =>
        {
            groupCalls++;
            return SeriesTitle();
        });

        GroupName();
        GroupName();
        Assert.Equal(1, groupCalls);

        var forgotten = new System.Collections.Generic.List<MetadataGuid>();
        table.Forgotten += forgotten.Add;
        table.Forget(Show);

        Assert.Equal([Show, Series, Group], forgotten);
        GroupName();
        Assert.Equal(2, groupCalls);
    }

    [Fact]
    public void AValueWorkedOutWithOlderSettingsIsWorkedOutAgain()
    {
        var table = new TextMemoTable();
        var calls = 0;

        table.Get(Series, TextMemoSlot.PreferredTitle, 0, () => ++calls);
        table.Get(Series, TextMemoSlot.PreferredTitle, 0, () => ++calls);
        Assert.Equal(2, table.Get(Series, TextMemoSlot.PreferredTitle, 1, () => ++calls));
    }

    [Fact]
    public void WhatAFilterMatchesByIsForgottenAloneAndForgetsNothingElse()
    {
        var table = new TextMemoTable();
        var titleCalls = 0;
        var nameCalls = 0;
        table.Get(Series, TextMemoSlot.PreferredTitle, 0, () => ++titleCalls);
        table.Get(Series, TextMemoSlot.FilterNames, 0, () =>
        {
            table.Record(Group);
            return ++nameCalls;
        });

        table.Forget(Group);
        table.Get(Series, TextMemoSlot.PreferredTitle, 0, () => ++titleCalls);
        table.Get(Series, TextMemoSlot.FilterNames, 0, () => ++nameCalls);
        Assert.Equal(1, titleCalls);
        Assert.Equal(2, nameCalls);

        table.ForgetFilters(Series);
        table.Get(Series, TextMemoSlot.FilterNames, 0, () => ++nameCalls);
        Assert.Equal(3, nameCalls);

        // The series' own texts changing drops what a filter matches it by.
        table.Forget(Series);
        table.Get(Series, TextMemoSlot.FilterNames, 0, () => ++nameCalls);
        Assert.Equal(4, nameCalls);
    }

    [Fact]
    public void AValueWorkedOutWhileWhatItReadWasForgottenIsNotKept()
    {
        var table = new TextMemoTable();
        var calls = 0;
        string Title() => table.Get(Series, TextMemoSlot.PreferredTitle, 0, () =>
        {
            calls++;
            table.Record(Anime);
            if (calls is 1)
                table.Forget(Anime);
            return "title";
        });

        Title();
        Title();
        Assert.Equal(2, calls);
        Title();
        Assert.Equal(2, calls);
    }

    [Fact]
    public void AValueWorkedOutWhileSomethingElseWasForgottenIsKept()
    {
        var table = new TextMemoTable();
        var calls = 0;
        string Title() => table.Get(Series, TextMemoSlot.PreferredTitle, 0, () =>
        {
            calls++;
            table.Record(Anime);
            table.Forget(Show);
            table.ForgetFilters(Group);
            return "title";
        });

        Title();
        Title();
        Assert.Equal(1, calls);
    }

    [Fact]
    public void AValueWorkedOutFromOneThatWasNotKeptIsNotKeptEither()
    {
        var table = new TextMemoTable();
        var seriesCalls = 0;
        var groupCalls = 0;
        string SeriesTitle() => table.Get(Series, TextMemoSlot.PreferredTitle, 0, () =>
        {
            seriesCalls++;
            table.Record(Show);
            if (seriesCalls is 1)
                table.Forget(Show);
            return "series";
        });
        string GroupName() => table.Get(Group, TextMemoSlot.Name, 0, () =>
        {
            groupCalls++;
            return SeriesTitle();
        });

        GroupName();
        GroupName();
        Assert.Equal(2, groupCalls);
        GroupName();
        Assert.Equal(2, groupCalls);
    }

    [Fact]
    public void AValueStartedBeforeTheForgetsWereClearedIsNotKept()
    {
        var table = new TextMemoTable();
        var calls = 0;
        string Title() => table.Get(Series, TextMemoSlot.PreferredTitle, 0, () =>
        {
            calls++;
            if (calls is 1)
                for (var i = 0; i < 5000; i++)
                    table.Forget(new(MetadataSource.TMDB, MetadataEntityType.Episode, i.ToString()));
            return "title";
        });

        Title();
        Title();
        Assert.Equal(2, calls);
        Title();
        Assert.Equal(2, calls);
    }

    [Fact]
    public void AskingForAValueWhileItIsWorkedOutAnswersWithTheFallback()
    {
        var table = new TextMemoTable();
        string Title() => table.Get(Series, TextMemoSlot.PreferredTitle, 0, () => "outer:" + Title(), () => "fallback");

        Assert.Equal("outer:fallback", Title());
        Assert.Equal("outer:fallback", table.Get<string>(Series, TextMemoSlot.PreferredTitle, 0, () => throw new InvalidOperationException()));
    }

    [Fact]
    public void AModelsHeldValuesAreReadUntilTheyAreForgotten()
    {
        var table = new TextMemoTable();
        object? handle = null;
        table.Get(Series, TextMemoSlot.PreferredTitle, 0, () =>
        {
            table.Record(Anime);
            return "title";
        }, null, ref handle);

        Assert.True(table.TryGet<string>(ref handle, TextMemoSlot.PreferredTitle, 0, out var title));
        Assert.Equal("title", title);
        Assert.False(table.TryGet<string>(ref handle, TextMemoSlot.PreferredTitle, 1, out _));
        Assert.False(table.TryGet<string>(ref handle, TextMemoSlot.Titles, 0, out _));
        Assert.False(new TextMemoTable().TryGet<string>(ref handle, TextMemoSlot.PreferredTitle, 0, out _));

        table.Forget(Anime);
        Assert.False(table.TryGet<string>(ref handle, TextMemoSlot.PreferredTitle, 0, out _));
    }

    private static MetadataGuid Episode(int number)
        => new(MetadataSource.Shoko, MetadataEntityType.Episode, number.ToString());

    [Fact]
    public void PastItsCapacityTheTableLetsTheEntriesReadLongestAgoGo()
    {
        var table = new TextMemoTable(10);
        for (var i = 0; i < 10; i++)
            table.Get(Episode(i), TextMemoSlot.PreferredTitle, 0, () => "title");
        for (var i = 0; i < 5; i++)
            Assert.True(table.TryGet<string>(Episode(i), TextMemoSlot.PreferredTitle, 0, out _));

        table.Get(Episode(10), TextMemoSlot.PreferredTitle, 0, () => "title");

        Assert.InRange(table.Count, 1, 10);
        foreach (var kept in (int[])[0, 1, 2, 3, 4, 10])
            Assert.True(table.TryGet<string>(Episode(kept), TextMemoSlot.PreferredTitle, 0, out _), $"Episode {kept} was let go.");
        Assert.Contains(Enumerable.Range(5, 5), gone => !table.TryGet<string>(Episode(gone), TextMemoSlot.PreferredTitle, 0, out _));
    }

    [Fact]
    public void AModelHoldingAValueLetGoWorksItOutAgain()
    {
        var table = new TextMemoTable(2);
        var calls = 0;
        object? handle = null;
        string Title() => table.Get(Series, TextMemoSlot.PreferredTitle, 0, () =>
        {
            calls++;
            return "title";
        }, null, ref handle);

        Title();
        table.Get(Episode(1), TextMemoSlot.PreferredTitle, 0, () => "one");
        table.Get(Episode(2), TextMemoSlot.PreferredTitle, 0, () => "two");

        Assert.False(table.TryGet<string>(ref handle, TextMemoSlot.PreferredTitle, 0, out _));
        Assert.Equal("title", Title());
        Assert.Equal(2, calls);
    }

    [Fact]
    public void AValueKeptPastATrimIsStillForgottenWithWhatItRead()
    {
        var table = new TextMemoTable(5);
        var calls = 0;
        string Title() => table.Get(Series, TextMemoSlot.PreferredTitle, 0, () =>
        {
            calls++;
            table.Record(Anime);
            return "title";
        });

        Title();
        for (var i = 0; i < 20; i++)
        {
            table.Get(Episode(i), TextMemoSlot.PreferredTitle, 0, () =>
            {
                table.Record(Show);
                return "episode";
            });
            Title();
        }

        Assert.Equal(1, calls);
        Assert.True(table.Count <= 5);

        table.Forget(Show);
        Title();
        Assert.Equal(1, calls);

        table.Forget(Anime);
        Title();
        Assert.Equal(2, calls);
    }

    [Fact]
    public void AValueWhoseMiddleOfTheChainWasLetGoIsStillForgottenWithTheStartOfIt()
    {
        var table = new TextMemoTable(5);
        var groupCalls = 0;
        string SeriesTitle() => table.Get(Series, TextMemoSlot.PreferredTitle, 0, () =>
        {
            table.Record(Show);
            return "series";
        });
        string GroupName() => table.Get(Group, TextMemoSlot.Name, 0, () =>
        {
            groupCalls++;
            return SeriesTitle();
        });

        GroupName();
        for (var i = 0; i < 20; i++)
        {
            table.Get(Episode(i), TextMemoSlot.PreferredTitle, 0, () => "episode");
            GroupName();
        }

        Assert.Equal(1, groupCalls);
        Assert.False(table.TryGet<string>(Series, TextMemoSlot.PreferredTitle, 0, out _));

        table.Forget(Show);
        GroupName();
        Assert.Equal(2, groupCalls);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Abstractions.Metadata.Text;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers the texts of a plugin source's entries once they are read through
/// the text manager: an entry's own texts against other sources', the people
/// and their other names, and the tags, studios, networks and orderings that
/// keep their name on their own row. What a user or another source adds on
/// top, and every write, reaches the reads.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class PluginEntryTextTests
{
    #region Helpers

    private static readonly MetadataSource _plugin = TestSources.Plugin;

    private static MetadataGuid ID(MetadataEntityType entityType, string id)
        => new(_plugin, entityType, id);

    private static TitleStub Title(string value, TitleLanguage language, string code, TitleType type, MetadataSource? source = null)
        => new() { Source = source ?? _plugin, Value = value, Language = language, LanguageCode = code, Type = type };

    private static TextStub Overview(string value, TitleLanguage language, string code, MetadataSource? source = null)
        => new() { Source = source ?? _plugin, Value = value, Language = language, LanguageCode = code };

    private static readonly IReadOnlyList<IReadOnlyList<MetadataSource>> _sourceOrders =
    [
        [MetadataSource.AniDB, MetadataSource.TMDB],
        [_plugin, MetadataSource.AniDB],
        [MetadataSource.AniDB, _plugin],
    ];

    /// <summary>
    /// Every store over in-memory tables, sharing one text store, with a text
    /// manager over them put in place for the models.
    /// </summary>
    private sealed class World : IDisposable
    {
        private readonly RepoFactoryScope _scope;

        public MetadataLookupTables Tables { get; } = new();

        public MetadataTextManager Manager { get; }

        public World()
        {
            Manager = TestTextManager.Build(Tables.TextStore, Tables.Service);
            _scope = Tables.Scope().Set(Manager);
        }

        public void Dispose()
            => _scope.Dispose();
    }

    #endregion

    #region Reading

    [Fact]
    public void AnotherSourcesTextInALowerLanguageNeverBeatsTheEntrysOwn()
    {
        var own = Title("Foo", TitleLanguage.English, "en", TitleType.Official);
        var ownMain = Title("Foo", TitleLanguage.Main, "x-main", TitleType.Main);
        IReadOnlyList<ITitle> others =
        [
            Title("Bar", TitleLanguage.Japanese, "ja", TitleType.Official, MetadataSource.User),
            Title("Bar", TitleLanguage.Japanese, "ja", TitleType.Official, MetadataSource.AniDB),
            Title("Bar", TitleLanguage.Japanese, "ja", TitleType.Official, TestSources.AniList),
        ];
        foreach (var other in others)
            foreach (var sourceOrder in _sourceOrders)
                foreach (var useSynonyms in new[] { false, true })
                    foreach (var episode in new[] { false, true })
                    {
                        var order = new List<MetadataSource>([.. sourceOrder, MetadataSource.User]);
                        Assert.Same(own, TextChooser.ChooseStoredTitle([own, other], _plugin, new([TitleLanguage.English, TitleLanguage.Japanese], order, useSynonyms, episode)));
                        Assert.Same(own, TextChooser.ChooseStoredTitle([other, own], _plugin, new([TitleLanguage.English, TitleLanguage.Japanese], order, useSynonyms, episode)));

                        // What a user or a ranked source adds in a higher
                        // language still reaches the choice.
                        var higher = TextChooser.ChooseStoredTitle([own, other], _plugin, new([TitleLanguage.Japanese, TitleLanguage.English], order, useSynonyms, episode));
                        Assert.Same(other.Source == TestSources.AniList ? own : other, higher);
                    }

        // x-main reads the entry's own main title before a user's.
        var userMain = Title("Bar", TitleLanguage.Main, "x-main", TitleType.Main, MetadataSource.User);
        foreach (var sourceOrder in _sourceOrders)
            Assert.Same(ownMain, TextChooser.ChooseStoredTitle([userMain, ownMain], _plugin, new([TitleLanguage.Main], [.. sourceOrder, MetadataSource.User], false, false)));

        var ownOverview = Overview("Foo.", TitleLanguage.English, "en");
        foreach (var source in new[] { MetadataSource.User, MetadataSource.TMDB, TestSources.AniList })
        {
            var otherOverview = Overview("Bar.", TitleLanguage.Japanese, "ja", source);
            foreach (var sourceOrder in _sourceOrders)
                Assert.Same(ownOverview, TextChooser.ChooseStoredOverview([otherOverview, ownOverview], _plugin, [TitleLanguage.English, TitleLanguage.Japanese], [.. sourceOrder, MetadataSource.User]));
        }
    }

    [Fact]
    public void APluginsPeopleReadTheirOverviewAndOtherNamesFromTheirRows()
    {
        using var world = new World();
        var tables = world.Tables;
        tables.PeopleStore.SaveCreators([
            new() { ID = ID(MetadataEntityType.Creator, "p1"), Name = "Kana", Overview = "A voice actress.", AlternativeNames = [new() { Name = "かな", LanguageCode = "ja" }, new() { Name = "Kana-chan" }] },
            new() { ID = ID(MetadataEntityType.Creator, "p2"), Name = "Hiro" },
        ]);
        tables.PeopleStore.SaveCharacters([new() { ID = ID(MetadataEntityType.Character, "x1"), Name = "Alice", Overview = "The lead.", AlternativeNames = [new() { Name = "Ally", LanguageCode = "en" }] }]);

        var kana = (ICreator)tables.PeopleStore.GetCreator(ID(MetadataEntityType.Creator, "p1"))!;
        var hiro = (ICreator)tables.PeopleStore.GetCreator(ID(MetadataEntityType.Creator, "p2"))!;
        var alice = (ICharacter)tables.PeopleStore.GetCharacter(ID(MetadataEntityType.Character, "x1"))!;

        Assert.Equal(["かな", "Kana-chan"], kana.AlternativeNames.Select(name => name.Value));
        Assert.Equal(["Ally"], alice.AlternativeNames.Select(name => name.Value));
        Assert.Empty(hiro.AlternativeNames);
        foreach (var (person, description) in new (IWithOverviews, string?)[] { (kana, "A voice actress."), (alice, "The lead."), (hiro, null) })
        {
            // The row's description is the default, preferred and only overview, in
            // English, whatever the language settings say.
            Assert.Equal(description, person.DefaultOverview?.Value);
            Assert.Equal(description, person.PreferredOverview?.Value);
            Assert.Equal(description is null ? [] : [description], person.Overviews.Select(text => text.Value));
            if (person.DefaultOverview is { } overview)
            {
                Assert.Equal((TitleLanguage.English, "en", _plugin), (overview.Language, overview.LanguageCode, overview.Source));
                Assert.True(overview.IsInlineDefault);
            }
        }
    }

    [Fact]
    public void TagsStudiosAndNetworksKeepTheirNamesOnTheirRowsAndOrderingsInTheStore()
    {
        using var world = new World();
        var tables = world.Tables;
        tables.StorePluginEntries();
        var ordering = Assert.Single(tables.OrderingService.GetStoredOrderings(_plugin));

        Assert.Equal("Tag", tables.TagStore.GetTag(ID(MetadataEntityType.Tag, "1"))!.Name);
        Assert.Equal("Studio", tables.StudioStore.GetStudio(ID(MetadataEntityType.Studio, "1"))!.Name);
        Assert.Equal("Network", tables.StudioStore.GetNetwork(ID(MetadataEntityType.Network, "1"))!.Name);
        Assert.Equal("Theirs", ordering.Title);
        Assert.Equal("All", Assert.Single(ordering.Seasons).Title);

        // The manager lists each name, kept on the row or stored.
        foreach (var (id, name, inline) in new[]
        {
            (ID(MetadataEntityType.Tag, "1"), "Tag", true),
            (ID(MetadataEntityType.Studio, "1"), "Studio", true),
            (ID(MetadataEntityType.Network, "1"), "Network", true),
            (ordering.ID, "Theirs", false),
            (ID(MetadataEntityType.Season, "g1"), "All", false),
        })
        {
            var title = Assert.Single(world.Manager.GetTitles(id));
            Assert.Equal((name, inline), (title.Value, title.IsInlineDefault));
            Assert.Equal(name, world.Manager.GetPreferredTitle(id)?.Value);
        }

        // A series' default ordering is built from its seasons, so its name is
        // never stored.
        var defaultOrdering = MetadataOrderingService.DefaultOrderingID(ID(MetadataEntityType.Series, "s1"));
        Assert.Empty(tables.Texts.GetRows(defaultOrdering));
    }

    #endregion

    #region Reading, with Picks and Additions

    [Fact]
    public void AUsersPicksAndFlagsAndOtherSourcesTextsReachAPluginEntrysModel()
    {
        using var world = new World();
        var tables = world.Tables;
        var seriesID = ID(MetadataEntityType.Series, "s1");
        tables.SeriesStore.SaveSeries(new()
        {
            ID = seriesID,
            Titles = [Title("Main", TitleLanguage.Main, "x-main", TitleType.Main), Title("English", TitleLanguage.English, "en", TitleType.Official)],
            Overviews = [Overview("About it.", TitleLanguage.English, "en")],
        });
        var series = (ISeries)tables.SeriesStore.GetSeries(seriesID)!;
        Assert.Equal("Main", series.Title);

        // Another source's titles come after the entry's own.
        world.Manager.SetTitles(seriesID, TestSources.AniList, [Title("Added", TitleLanguage.English, "en", TitleType.Official, TestSources.AniList)]);
        Assert.Equal(["Main", "English", "Added"], series.Titles.Select(title => title.Value));

        // A user's overall pick wins, whoever gave the title.
        var pick = world.Manager.SetPreferredTitle(seriesID, series.Titles[2]);
        Assert.Equal(pick.ID, series.PreferredTitle?.ID);
        Assert.Equal("Added", series.Title);

        // A disabled title is neither listed nor the default, so the synthesized
        // default stands in for the main title.
        world.Manager.UnsetPreferredText(pick);
        world.Manager.EnableText(series.Titles[0], false);
        Assert.DoesNotContain(series.Titles, title => title.Value == "Main");
        Assert.Equal(["TestPlugin Series s1", "English", "Added"], series.Titles.Select(title => title.Value));
        Assert.True(series.DefaultTitle.IsSynthesized);
        Assert.Equal("TestPlugin Series s1", series.Title);

        var overview = world.Manager.SetPreferredOverview(seriesID, Overview("Mine.", TitleLanguage.English, "en", MetadataSource.User));
        Assert.Equal(overview.ID, series.PreferredOverview?.ID);
        Assert.Equal("About it.", series.DefaultOverview?.Value);
    }

    [Fact]
    public void APersonsPicksReachItsModelButNeverItsOtherNamesWhichAreItsOwnSourcesOnly()
    {
        using var world = new World();
        var tables = world.Tables;
        var personID = ID(MetadataEntityType.Creator, "p1");
        tables.PeopleStore.SaveCreators([new() { ID = personID, Name = "Kana", Overview = "A voice actress.", AlternativeNames = [new() { Name = "かな", LanguageCode = "ja" }] }]);
        var person = (ICreator)tables.PeopleStore.GetCreator(personID)!;

        world.Manager.SetPreferredTitle(personID, person.AlternativeNames[0]);
        world.Manager.AddText(personID, new TextData { Kind = TextKind.Title, Value = "Kana-san", LanguageCode = "en", Source = MetadataSource.User });
        world.Manager.SetTitles(personID, TestSources.AniList, [Title("Kana A.", TitleLanguage.English, "en", TitleType.Official, TestSources.AniList)]);
        var overview = world.Manager.SetPreferredOverview(personID, Overview("Mine.", TitleLanguage.English, "en", MetadataSource.User));

        Assert.Equal(["かな"], person.AlternativeNames.Select(name => name.Value));
        Assert.Equal("Kana", person.Name);
        Assert.Equal(overview.ID, person.PreferredOverview?.ID);
        Assert.Equal(["A voice actress.", "Mine."], person.Overviews.Select(text => text.Value));
        Assert.Equal("A voice actress.", person.DefaultOverview?.Value);
    }

    #endregion

    #region Writing

    [Fact]
    public void SavingATagStudioNetworkOrOrderingOutdatesTheChoiceRememberedForIt()
    {
        using var world = new World();
        var tables = world.Tables;
        tables.StorePluginEntries();
        var tag = ID(MetadataEntityType.Tag, "1");
        var studio = ID(MetadataEntityType.Studio, "1");
        var network = ID(MetadataEntityType.Network, "1");
        var ordering = ID(MetadataEntityType.Ordering, "o1");
        var group = ID(MetadataEntityType.Season, "g1");
        foreach (var id in new[] { tag, studio, network, ordering, group })
            Assert.NotNull(world.Manager.GetPreferredTitle(id));
        Assert.Null(world.Manager.GetPreferredOverview(tag));

        tables.TagStore.SaveTags([new() { ID = tag, Name = "Renamed Tag", Overview = "What it means." }]);
        tables.StudioStore.SaveStudios([new() { ID = studio, Name = "Renamed Studio" }]);
        tables.StudioStore.SaveNetworks([new() { ID = network, Name = "Renamed Network" }]);
        tables.OrderingService.SaveOrdering(new()
        {
            ID = ordering,
            SeriesID = ID(MetadataEntityType.Series, "s1"),
            Titles = TestTexts.Named("Renamed Ordering"),
            Groups = [new() { ID = group, Titles = TestTexts.Named("Renamed Group"), Episodes = [ID(MetadataEntityType.Episode, "e1")] }],
        });

        Assert.Equal("Renamed Tag", world.Manager.GetPreferredTitle(tag)?.Value);
        Assert.Equal("What it means.", world.Manager.GetPreferredOverview(tag)?.Value);
        Assert.Equal("Renamed Studio", world.Manager.GetPreferredTitle(studio)?.Value);
        Assert.Equal("Renamed Network", world.Manager.GetPreferredTitle(network)?.Value);
        Assert.Equal("Renamed Ordering", world.Manager.GetPreferredTitle(ordering)?.Value);
        Assert.Equal("Renamed Group", world.Manager.GetPreferredTitle(group)?.Value);
    }

    [Fact]
    public void RemovingStudiosNetworksAndOrderingsTakesTheirTexts()
    {
        using var world = new World();
        var tables = world.Tables;
        tables.StorePluginEntries();
        var studio = ID(MetadataEntityType.Studio, "1");
        var network = ID(MetadataEntityType.Network, "1");
        var ordering = ID(MetadataEntityType.Ordering, "o1");
        var group = ID(MetadataEntityType.Season, "g1");
        tables.StudioStore.SaveStudios([new() { ID = studio, Name = "Studio" }]);
        tables.StudioStore.SaveNetworks([new() { ID = network, Name = "Network" }]);
        foreach (var id in new[] { studio, network, ordering, group })
            world.Manager.AddText(id, new TextData { Kind = TextKind.Title, Value = "Mine", LanguageCode = "en" });

        Assert.Equal(2, tables.StudioStore.RemoveOrphaned(_plugin, DateTime.Now.AddMinutes(1)).Count);
        Assert.True(tables.OrderingService.RemoveOrdering(ordering));

        Assert.Empty(tables.Texts.GetRows(studio));
        Assert.Empty(tables.Texts.GetRows(network));
        Assert.Empty(tables.Texts.GetRows(ordering));
        Assert.Empty(tables.Texts.GetRows(group));
    }

    #endregion
}

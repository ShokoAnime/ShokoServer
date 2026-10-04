using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Events;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Abstractions.Metadata.Text;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Models.Shoko.Embedded;
using Shoko.Server.Repositories.Cached.Metadata.Text;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers what <see cref="MetadataTextManager"/> does for any entry by its ID:
/// the defaults kept on an entry's row, the user's picks and flags, the
/// choice it remembers, the events it raises and the purge of texts whose
/// entry is gone.
/// </summary>
public class MetadataTextManagerStoreTests
{
    #region Helpers

    private static readonly MetadataGuid _series = new(TestSources.Plugin, MetadataEntityType.Series, "text-1");

    private static readonly MetadataGuid _other = new(TestSources.Plugin, MetadataEntityType.Series, "text-2");

    private static readonly MetadataGuid _episode = new(TestSources.Plugin, MetadataEntityType.Episode, "text-e1");

    private static readonly MetadataGuid _person = new(TestSources.Plugin, MetadataEntityType.Creator, "p5");

    private static readonly MetadataGuid _anidbEpisode = new(MetadataSource.AniDB, MetadataEntityType.Episode, "101");

    private static readonly MetadataGuid _shokoEpisode = new(MetadataSource.Shoko, MetadataEntityType.Episode, "34");

    private static TitleStub Title(string value, TitleLanguage language = TitleLanguage.English, string code = "en", TitleType type = TitleType.Official)
        => new() { Source = TestSources.Plugin, Value = value, Language = language, LanguageCode = code, Type = type };

    private static TextStub Overview(string value)
        => new() { Source = TestSources.Plugin, Value = value, Language = TitleLanguage.English, LanguageCode = "en" };

    private sealed class Harness
    {
        public TextCache Cache { get; } = new();

        public Mock<IMetadataService> Service { get; } = new();

        public MetadataTextStore Store { get; }

        public MetadataTextManager Manager { get; }

        public Harness()
        {
            StubSettingsProvider.Install();
            Store = new(Cache, new CacheOnlyRowWriter());
            Manager = new(Service.Object, Store, NullLogger<MetadataTextManager>.Instance);
        }

        public void Holds(MetadataGuid id, IMetadata entry)
            => Service.Setup(service => service.GetEntry(id)).Returns(entry);
    }

    private static IMetadata Entry(MetadataGuid id)
    {
        var entry = new Mock<IMetadata>();
        entry.SetupGet(e => e.ID).Returns(id);
        return entry.Object;
    }

    private static Metadata_Creator Person(string name = "Person", string? overview = "About them.")
        => new() { Source = TestSources.Plugin, ProviderID = "p5", Name = name, Description = overview };

    /// <summary>
    /// An episode whose model keeps its own texts, as the core's sources do.
    /// </summary>
    private static Mock<T> ModelEpisode<T>(MetadataGuid id, int number, Func<IReadOnlyList<ITitle>> titles, ITitle? preferred, IText? overview = null)
        where T : class, IEpisode
    {
        var episode = new Mock<T>();
        episode.SetupGet(e => e.ID).Returns(id);
        episode.SetupGet(e => e.Type).Returns(EpisodeType.Episode);
        episode.SetupGet(e => e.EpisodeNumber).Returns(number);
        episode.SetupGet(e => e.Titles).Returns(titles);
        episode.SetupGet(e => e.PreferredTitle).Returns(preferred);
        episode.SetupGet(e => e.DefaultTitle).Returns(preferred ?? new TitleStub { Source = id.Source, Value = string.Empty, Language = TitleLanguage.Unknown, LanguageCode = "unk" });
        episode.SetupGet(e => e.Overviews).Returns(overview is null ? [] : [overview]);
        episode.SetupGet(e => e.PreferredOverview).Returns(overview);
        episode.SetupGet(e => e.DefaultOverview).Returns(overview);
        return episode;
    }

    /// <summary>
    /// A core entry whose model keeps its own lists and a default on its row.
    /// </summary>
    private sealed class CoreEntry(MetadataGuid id, IReadOnlyList<ITitle> titles, IReadOnlyList<IText> overviews, ITitle? inlineTitle, IText? inlineOverview)
        : IMetadata, IWithTitles, IWithOverviews, IInlineTextSource
    {
        public MetadataGuid ID => id;

        public string Title => DefaultTitle.Value;

        public ITitle DefaultTitle => inlineTitle ?? titles[0];

        public ITitle? PreferredTitle => DefaultTitle;

        public IReadOnlyList<ITitle> Titles => titles;

        public IText? DefaultOverview => inlineOverview;

        public IText? PreferredOverview => inlineOverview;

        public IReadOnlyList<IText> Overviews => overviews;

        public ITitle? InlineTitle => inlineTitle;

        public IText? InlineOverview => inlineOverview;
    }

    #endregion

    #region Reading

    [Fact]
    public void AnEntrysDefaultOnItsRowIsListedAheadOfItsStoredTexts()
    {
        var harness = new Harness();
        harness.Holds(_person, Person("Show", "About it."));
        harness.Manager.SetTitles(_person, TestSources.AniList, [Title("Added")]);

        var titles = harness.Manager.GetTitles(_person);
        Assert.Equal(["Show", "Added"], titles.Select(title => title.Value));
        Assert.True(titles[0].IsInlineDefault);
        Assert.Null(titles[0].ID);
        Assert.Equal(TitleLanguage.Unknown, titles[0].Language);
        Assert.Equal(["Added"], harness.Manager.GetTitles(_person, new() { IncludeInlineDefault = false }).Select(title => title.Value));
        Assert.Equal("Show", harness.Manager.GetDefaultTitle(_person)?.Value);
        Assert.Equal("About it.", harness.Manager.GetDefaultOverview(_person)?.Value);
        Assert.Equal("About it.", Assert.Single(harness.Manager.GetOverviews(_person)).Value);
    }

    [Fact]
    public void ACoreEntrysDefaultOnItsRowIsListedOrLeftOutAsAsked()
    {
        var harness = new Harness();
        var show = new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Series, "7");
        var english = InlineText.Title(MetadataSource.AniDB, "Show", TitleLanguage.EnglishAmerican, "en", "US", TitleType.Official)!;
        var french = new TitleStub { Source = MetadataSource.AniDB, Value = "Série", Language = TitleLanguage.French, LanguageCode = "fr", Type = TitleType.Official };
        var described = InlineText.Overview(MetadataSource.AniDB, "About it.", TitleLanguage.EnglishAmerican, "en", "US")!;
        harness.Holds(show, new CoreEntry(show, [french], [described], english, described));

        // The default on the row is listed first, unless left out.
        Assert.Equal(["Show", "Série"], harness.Manager.GetTitles(show).Select(title => title.Value));
        Assert.Equal(["Série"], harness.Manager.GetTitles(show, new() { IncludeInlineDefault = false }).Select(title => title.Value));
        Assert.Equal(["About it."], harness.Manager.GetOverviews(show).Select(overview => overview.Value));
        Assert.Empty(harness.Manager.GetOverviews(show, new() { IncludeInlineDefault = false }));
    }

    [Fact]
    public void WithoutADefaultOnItsRowAnEntryIsCalledByItsOwnSourcesMainTitle()
    {
        var harness = new Harness();
        harness.Manager.SetTitles(_series, TestSources.Plugin, [Title("Official"), Title("Main", TitleLanguage.Romaji, "x-jat", TitleType.Main)]);
        harness.Manager.SetTitles(_series, TestSources.AniList, [Title("Theirs", type: TitleType.Main)]);

        Assert.Equal("Main", harness.Manager.GetDefaultTitle(_series)?.Value);
        Assert.Null(harness.Manager.GetDefaultTitle(_other));
    }

    [Fact]
    public void ADisabledTextIsOnlyListedWhenAskedFor()
    {
        var harness = new Harness();
        harness.Manager.SetTitles(_series, TestSources.Plugin, [Title("Kept"), Title("Hidden", TitleLanguage.Japanese, "ja")]);
        harness.Manager.EnableText(harness.Manager.GetTitles(_series).Single(title => title.Value == "Hidden"), false);

        Assert.Equal(["Kept"], harness.Manager.GetTitles(_series).Select(title => title.Value));
        Assert.Equal(["Hidden"], harness.Manager.GetTitles(_series, new() { IsEnabled = false }).Select(title => title.Value));
        Assert.Equal(2, harness.Manager.GetTitles(_series, new() { IsEnabled = null }).Count);
    }

    [Fact]
    public void AnEntrysOwnSourceIsReadFirstAndItsDefaultIsTheFallback()
    {
        var harness = new Harness();
        harness.Manager.SetTitles(_series, TestSources.Plugin, [Title("Unranked", TitleLanguage.Swedish, "sv", TitleType.Main)]);

        // Its own source answers x-main even though no one ranked it, and a
        // title in no preferred language still leaves the default.
        Assert.Equal("Unranked", harness.Manager.GetPreferredTitle(_series)?.Value);
    }

    [Fact]
    public void AnEpisodeWithNoTitleAtAllGetsASynthesizedOne()
    {
        var harness = new Harness();
        var episode = new Mock<IEpisode>();
        episode.SetupGet(e => e.ID).Returns(_episode);
        episode.SetupGet(e => e.Type).Returns(EpisodeType.Episode);
        episode.SetupGet(e => e.EpisodeNumber).Returns(12);
        harness.Holds(_episode, episode.Object);

        var title = harness.Manager.GetPreferredTitle(_episode);

        Assert.NotNull(title);
        Assert.True(title.IsSynthesized);
        Assert.Contains("12", title.Value);
        Assert.Empty(harness.Manager.GetTitles(_episode));
    }

    [Fact]
    public void AnAnidbEpisodeIsReadThroughItsOwnModelAndNeverGetsASynthesizedTitle()
    {
        var harness = new Harness();
        var real = new TitleStub { Source = MetadataSource.AniDB, Value = "The Real One", Language = TitleLanguage.English, LanguageCode = "en" };
        var overview = new TextStub { Source = MetadataSource.AniDB, Value = "What happens.", Language = TitleLanguage.English, LanguageCode = "en" };
        harness.Holds(_anidbEpisode, ModelEpisode<IEpisode>(_anidbEpisode, 5, () => [real], real, overview).Object);
        harness.Manager.SetTitles(_anidbEpisode, TestSources.Plugin, [Title("Added")]);

        Assert.Equal(["The Real One", "Added"], harness.Manager.GetTitles(_anidbEpisode).Select(title => title.Value));
        Assert.Equal(["Added"], harness.Manager.GetTitles(_anidbEpisode, new() { Source = TestSources.Plugin }).Select(title => title.Value));
        var preferred = harness.Manager.GetPreferredTitle(_anidbEpisode);
        Assert.Equal("The Real One", preferred?.Value);
        Assert.False(preferred?.IsSynthesized);
        Assert.Equal("The Real One", harness.Manager.GetDefaultTitle(_anidbEpisode)?.Value);
        Assert.Equal("What happens.", harness.Manager.GetPreferredOverview(_anidbEpisode)?.Value);
        Assert.Equal("What happens.", harness.Manager.GetDefaultOverview(_anidbEpisode)?.Value);

        // A user's overall pick still wins over the model's choice.
        var pick = harness.Manager.SetPreferredTitle(_anidbEpisode, harness.Manager.GetTitles(_anidbEpisode).Single(title => title.Value == "Added"));
        Assert.Equal(pick.ID, harness.Manager.GetPreferredTitle(_anidbEpisode)?.ID);
    }

    [Fact]
    public void AShokoEpisodeListsWhatItsModelHoldsOnceAndOnlyAnEpisodeWithNoTitlesIsSynthesized()
    {
        var harness = new Harness();
        var real = new TitleStub { Source = MetadataSource.AniDB, Value = "Its Name", Language = TitleLanguage.English, LanguageCode = "en" };
        harness.Manager.SetTitles(_shokoEpisode, TestSources.Plugin, [Title("Contributed")]);

        // The Shoko model lists the contributions itself, so they are not
        // listed twice.
        harness.Holds(_shokoEpisode, ModelEpisode<IShokoEpisode>(
            _shokoEpisode,
            3,
            () => [real, .. harness.Manager.GetContributedTitles(Entry(_shokoEpisode))],
            real
        ).Object);
        Assert.Equal(["Its Name", "Contributed"], harness.Manager.GetTitles(_shokoEpisode).Select(title => title.Value));
        Assert.Equal("Its Name", harness.Manager.GetPreferredTitle(_shokoEpisode)?.Value);

        // Only an episode with no titles of its own gets a synthesized one.
        var bare = new MetadataGuid(MetadataSource.Shoko, MetadataEntityType.Episode, "35");
        harness.Holds(bare, ModelEpisode<IShokoEpisode>(bare, 4, () => [], null).Object);
        var synthesized = harness.Manager.GetPreferredTitle(bare);
        Assert.True(synthesized?.IsSynthesized);
        Assert.Contains("4", synthesized?.Value);
    }

    [Fact]
    public void ADisabledContributionOnAShokoEntryIsNeitherListedNorChosen()
    {
        var harness = new Harness();
        harness.Manager.SetTitles(_shokoEpisode, TestSources.Plugin, [Title("Shown"), Title("Hidden", TitleLanguage.Japanese, "ja")]);
        var hidden = harness.Store.GetTitles(_shokoEpisode).Single(title => title.Value == "Hidden");

        harness.Manager.EnableText(hidden, false);

        // The Shoko walks read the contributions through here, and pick from them.
        var episode = Entry(_shokoEpisode);
        Assert.Equal(["Shown"], harness.Manager.GetContributedTitles(episode).Select(title => title.Value));
        Assert.Null(MetadataTextManager.Pick(harness.Manager.GetContributedTitles(episode), TitleLanguage.Japanese, useSynonyms: true));

        var season = new AnimeSeason(new AnimeSeries { AnimeSeriesID = 12 }, EpisodeType.Special, 0);
        harness.Manager.SetTitles(((IMetadata)season).ID, TestSources.Plugin, [Title("Season"), Title("Off", TitleLanguage.Japanese, "ja")]);
        harness.Manager.EnableText(harness.Store.GetTitles(((IMetadata)season).ID).Single(title => title.Value == "Off"), false);
        Assert.Equal(["Season"], harness.Manager.SeasonTitlesOf(season).Select(title => title.Value));
    }

    [Fact]
    public void TheChoiceIsRememberedUntilTheEntrysTextsChange()
    {
        // A change of the language settings outdates it too, but that is a
        // process-wide bump, left to the serialised TextInvalidationTests.
        var harness = new Harness();
        harness.Manager.SetTitles(_series, TestSources.Plugin, [Title("First", TitleLanguage.Main, "x-main", TitleType.Main)]);
        var first = harness.Manager.GetPreferredTitle(_series);

        Assert.Same(first, harness.Manager.GetPreferredTitle(_series));

        harness.Manager.SetTitles(_series, TestSources.Plugin, [Title("Second", TitleLanguage.Main, "x-main", TitleType.Main)]);
        Assert.Equal("Second", harness.Manager.GetPreferredTitle(_series)?.Value);
    }

    [Fact]
    public void AnUnrankedOwnSourcesOverviewIsStillChosen()
    {
        var harness = new Harness();
        harness.Manager.SetOverviews(_series, TestSources.Plugin, [Overview("Its own.")]);

        Assert.Equal("Its own.", harness.Manager.GetPreferredOverview(_series)?.Value);
    }

    #endregion

    #region Generic Titles

    private static IEpisode NumberedEpisode(MetadataGuid id, EpisodeType type, int number)
        => Mock.Of<IEpisode>(episode => episode.ID == id && episode.Type == type && episode.EpisodeNumber == number);

    private static ISeason NumberedSeason(MetadataGuid id, int number)
        => Mock.Of<ISeason>(season => season.ID == id && season.SeasonNumber == number);

    [Fact]
    public void AWriteLeavesOutOnlyTheGenericTitlesCarryingTheEntrysOwnNumber()
    {
        var harness = new Harness();
        var season = new MetadataGuid(TestSources.Plugin, MetadataEntityType.Season, "text-s1");
        var unknown = new MetadataGuid(TestSources.Plugin, MetadataEntityType.Episode, "text-e9");
        harness.Holds(_episode, NumberedEpisode(_episode, EpisodeType.Episode, 12));
        harness.Holds(season, NumberedSeason(season, 8));
        harness.Holds(_anidbEpisode, NumberedEpisode(_anidbEpisode, EpisodeType.Special, 5));
        harness.Manager.SetTitles(
            _episode,
            TestSources.Plugin,
            [Title("Episode 12"), Title("Folge 12"), Title("TBA"), Title("Episode 13"), Title("Episode S12"), Title("Opening 2")]
        );
        harness.Manager.SetTitles(season, TestSources.AniList, [Title("Staffel 8"), Title("Season 11"), Title("Specials")]);
        harness.Manager.SetTitles(_anidbEpisode, MetadataSource.AniDB, [Title("Episode S5"), Title("Episode S2"), Title("Special 5")]);
        harness.Manager.SetTitles(unknown, TestSources.Plugin, [Title("TBD"), Title("Episode 3")]);

        Assert.Equal(["Episode 13", "Episode S12", "Opening 2"], harness.Store.GetTitles(_episode).Select(title => title.Value));
        Assert.Equal(["Season 11", "Specials"], harness.Store.GetTitles(season).Select(title => title.Value));
        Assert.Equal(["Episode S2", "Special 5"], harness.Store.GetTitles(_anidbEpisode).Select(title => title.Value));
        Assert.Equal(["Episode 3"], harness.Store.GetTitles(unknown).Select(title => title.Value));
    }

    [Fact]
    public void AddingOrPickingAGenericTitleOfAnyEpisodeThrows()
    {
        var harness = new Harness();
        harness.Holds(_episode, NumberedEpisode(_episode, EpisodeType.Episode, 5));
        harness.Holds(_anidbEpisode, NumberedEpisode(_anidbEpisode, EpisodeType.Episode, 5));
        harness.Manager.SetTitles(_episode, TestSources.Plugin, [Title("Real")]);

        var generic = new TextData { Kind = TextKind.Title, Value = "Episode 5", LanguageCode = "en" };
        Assert.Throws<ArgumentException>(() => harness.Manager.AddText(_anidbEpisode, generic));
        Assert.Throws<ArgumentException>(() => harness.Manager.AddText(_episode, new() { Kind = TextKind.Title, Value = "第5話", LanguageCode = "ja" }));
        Assert.Throws<ArgumentException>(() => harness.Manager.SetPreferredTitle(_episode, Title("Episode 5")));
        var mine = harness.Manager.AddText(_episode, new() { Kind = TextKind.Title, Value = "Mine", LanguageCode = "en" });
        Assert.Throws<ArgumentException>(() => harness.Manager.UpdateText(mine, new() { Value = "Episode 5" }));
        Assert.Equal("Episode 13", harness.Manager.UpdateText(mine, new() { Value = "Episode 13" }).Value);
    }

    #endregion

    #region User Writes

    [Fact]
    public void PickingTheDefaultOnARowStoresAUserPickThatWinsOverall()
    {
        var harness = new Harness();
        harness.Holds(_person, Person("Show"));
        harness.Manager.SetTitles(_person, TestSources.AniList, [Title("Added")]);
        var inline = harness.Manager.GetTitles(_person)[0];

        var pick = harness.Manager.SetPreferredTitle(_person, inline);

        Assert.Equal(MetadataSource.User, pick.Source);
        Assert.Null(pick.ReferenceID);
        Assert.Equal(TextPreference.Overall, pick.Preference);
        Assert.Equal(pick.ID, harness.Manager.GetPreferredTitle(_person)?.ID);
    }

    [Fact]
    public void ATitlePreferredAsAnOverviewIsCopiedByValueAndNotFollowed()
    {
        var harness = new Harness();
        harness.Manager.SetTitles(_other, TestSources.Plugin, [Title("A title")]);
        harness.Manager.SetOverviews(_other, TestSources.Plugin, [Overview("Unrelated.")]);
        var title = harness.Manager.GetTitles(_other).Single();

        var pick = harness.Manager.SetPreferredOverview(_series, title);

        Assert.Null(pick.ReferenceID);
        Assert.Equal("A title", pick.Value);
        Assert.IsNotAssignableFrom<ITitle>(pick);

        // Removing the overview that shares the title's ID leaves the pick.
        harness.Manager.RemoveText(harness.Manager.GetOverviews(_other).Single());
        Assert.Equal("A title", Assert.Single(harness.Manager.GetOverviews(_series)).Value);
    }

    [Fact]
    public void SettingTheUserSourcesTitlesKeepsTheUsersPicks()
    {
        var harness = new Harness();
        harness.Manager.SetTitles(_other, TestSources.Plugin, [Title("Theirs")]);
        var pick = harness.Manager.SetPreferredTitle(_series, harness.Manager.GetTitles(_other).Single());

        harness.Manager.SetTitles(_series, MetadataSource.User, [Title("Typed")]);

        var titles = harness.Manager.GetTitles(_series);
        Assert.Equal(["Theirs", "Typed"], titles.Select(title => title.Value).Order());
        var kept = titles.Single(title => title.ID == pick.ID);
        Assert.Equal(pick.ReferenceID, kept.ReferenceID);
        Assert.Equal("Typed", titles.Single(title => title.ID != pick.ID).Value);
    }

    [Fact]
    public void AnOverallPickTakesThePlaceOfTheLastAndALanguagePickOnlyOfItsLanguage()
    {
        var harness = new Harness();
        harness.Manager.SetTitles(_series, TestSources.Plugin, [Title("A"), Title("B"), Title("C", TitleLanguage.Japanese, "ja"), Title("D", TitleLanguage.Japanese, "ja")]);
        ITitle Get(string value) => harness.Manager.GetTitles(_series).Single(title => title.Value == value);

        harness.Manager.SetPreferredTitle(_series, Get("A"));
        harness.Manager.SetPreferredTitle(_series, Get("B"));
        harness.Manager.SetPreferredTitle(_series, Get("C"), forLanguageOnly: true);
        harness.Manager.SetPreferredTitle(_series, Get("A"), forLanguageOnly: true);
        harness.Manager.SetPreferredTitle(_series, Get("D"), forLanguageOnly: true);

        Assert.Equal(
            [TextPreference.Language, TextPreference.Overall, TextPreference.None, TextPreference.Language],
            harness.Manager.GetTitles(_series).OrderBy(title => title.Value).Select(title => title.Preference)
        );
        Assert.Equal("B", harness.Manager.GetPreferredTitle(_series)?.Value);
    }

    [Fact]
    public void UnsettingAPickOfAnotherTextRemovesItAndUnsettingAFlagKeepsTheText()
    {
        var harness = new Harness();
        harness.Manager.SetTitles(_series, TestSources.Plugin, [Title("Own")]);
        harness.Manager.SetTitles(_other, TestSources.Plugin, [Title("Theirs")]);
        var own = harness.Manager.SetPreferredTitle(_series, harness.Manager.GetTitles(_series).Single());
        var pick = harness.Manager.SetPreferredTitle(_series, harness.Manager.GetTitles(_other).Single(), forLanguageOnly: true);

        Assert.True(harness.Manager.UnsetPreferredText(pick));
        Assert.True(harness.Manager.UnsetPreferredText(own));
        Assert.False(harness.Manager.UnsetPreferredText(own));

        var left = Assert.Single(harness.Manager.GetTitles(_series));
        Assert.Equal(("Own", TextPreference.None), (left.Value, left.Preference));
    }

    [Fact]
    public void UnsettingEveryPreferenceOfAnEntryCountsThem()
    {
        var harness = new Harness();
        harness.Manager.SetTitles(_series, TestSources.Plugin, [Title("A")]);
        harness.Manager.SetOverviews(_series, TestSources.Plugin, [Overview("B")]);
        harness.Manager.SetPreferredTitle(_series, harness.Manager.GetTitles(_series).Single());
        harness.Manager.SetPreferredOverview(_series, harness.Manager.GetOverviews(_series).Single());
        harness.Manager.SetPreferredTitle(_series, Title("Typed"), forLanguageOnly: true);

        Assert.Equal(1, harness.Manager.UnsetAllPreferredTexts(_series, TextKind.Overview));
        Assert.Equal(2, harness.Manager.UnsetAllPreferredTexts(_series));
        Assert.All(harness.Manager.GetTitles(_series), title => Assert.Equal(TextPreference.None, title.Preference));
    }

    [Fact]
    public void AUserMayAddAndRewordTheirOwnTextButNotASources()
    {
        var harness = new Harness();
        harness.Manager.SetTitles(_series, TestSources.Plugin, [Title("Source")]);
        var added = harness.Manager.AddText(_series, new() { Kind = TextKind.Title, Value = "Mine", LanguageCode = "de", TitleType = TitleType.Official });

        Assert.Equal((MetadataSource.User, TitleLanguage.German), (added.Source, added.Language));

        var reworded = harness.Manager.UpdateText(added, new() { Value = "Still mine" });
        Assert.Equal("Still mine", reworded.Value);
        Assert.Equal(added.ID, reworded.ID);
        Assert.Throws<InvalidOperationException>(() => harness.Manager.UpdateText(harness.Manager.GetTitles(_series).First(), new() { Value = "No" }));
        Assert.Throws<ArgumentException>(() => harness.Manager.UpdateText(Title("Not stored"), new() { IsEnabled = false }));
        Assert.Throws<ArgumentException>(() => harness.Manager.AddText(_series, new() { Kind = TextKind.Title, Value = " ", LanguageCode = "en" }));
    }

    [Fact]
    public void RemovingATextTakesItsPicksAlong()
    {
        var harness = new Harness();
        harness.Manager.SetTitles(_other, TestSources.Plugin, [Title("Theirs")]);
        var theirs = harness.Manager.GetTitles(_other).Single();
        harness.Manager.SetPreferredTitle(_series, theirs);

        Assert.True(harness.Manager.RemoveText(theirs));
        Assert.False(harness.Manager.RemoveText(theirs));
        Assert.Empty(harness.Manager.GetTitles(_series));
        Assert.Empty(harness.Manager.GetTitles(_other));
    }

    #endregion

    #region Events

    [Fact]
    public void EveryWriteTellsOncePerEntryAndEachUserWriteTellsAboutItsText()
    {
        var harness = new Harness();
        var changed = new List<EntityTextsChangedEventArgs>();
        var added = new List<IText>();
        var updated = new List<IText>();
        var removed = new List<IText>();
        harness.Manager.EntityTextsChanged += (_, args) => changed.Add(args);
        harness.Manager.TextAdded += (_, args) => added.Add(args.Text);
        harness.Manager.TextUpdated += (_, args) => updated.Add(args.Text);
        harness.Manager.TextRemoved += (_, args) => removed.Add(args.Text);

        harness.Manager.SetTitles(_series, TestSources.Plugin, [Title("A"), Title("B")]);
        var text = harness.Manager.AddText(_series, new() { Kind = TextKind.Overview, Value = "Mine", LanguageCode = "en" });
        harness.Manager.EnableText(text, false);
        harness.Manager.RemoveText(text);

        Assert.Equal(4, changed.Count);
        Assert.All(changed, args => Assert.Equal(_series, args.EntityID));
        Assert.Equal([TextKind.Title], changed[0].Kinds);
        Assert.Equal([TestSources.Plugin], changed[0].Sources);
        Assert.Equal([text.ID], added.Select(t => t.ID));
        Assert.False(Assert.Single(updated).IsEnabled);
        Assert.Equal(text.ID, Assert.Single(removed).ID);
    }

    #endregion

    #region Maintenance

    [Fact]
    public void TheTextsOfAnEntryThatIsGoneArePurged()
    {
        var harness = new Harness();
        harness.Holds(_series, Entry(_series));
        harness.Manager.SetTitles(_series, TestSources.Plugin, [Title("Kept")]);
        harness.Manager.SetTitles(_other, TestSources.Plugin, [Title("Gone"), Title("Also gone", TitleLanguage.Japanese, "ja")]);
        harness.Manager.SetOverviews(_other, TestSources.Plugin, [Overview("Gone too")]);

        Assert.Equal([_other], harness.Manager.GetOrphanedEntries());
        Assert.Empty(harness.Manager.GetOrphanedEntries(MetadataSource.TMDB));
        Assert.Equal(3, harness.Manager.PurgeOrphanedTexts());
        Assert.Empty(harness.Manager.GetOrphanedEntries());
        Assert.Equal("Kept", Assert.Single(harness.Manager.GetTitles(_series)).Value);
    }

    [Fact]
    public void ATextIsFoundByItsIDAndEveryTextCanBeListed()
    {
        var harness = new Harness();
        harness.Manager.SetTitles(_series, TestSources.Plugin, [Title("A")]);
        harness.Manager.SetOverviews(_other, TestSources.AniList, [Overview("B")]);
        var title = harness.Manager.GetTitles(_series).Single();
        var overview = harness.Manager.GetOverviews(_other).Single();

        Assert.Equal("A", harness.Manager.GetTitleByID(title.ID!.Value)?.Value);
        Assert.Equal("B", harness.Manager.GetOverviewByID(overview.ID!.Value)?.Value);
        Assert.Null(harness.Manager.GetOverviewByID(title.ID!.Value + 1000));
        Assert.Equal(["A"], harness.Manager.GetAllTexts(TextKind.Title).Select(text => text.Value));
        Assert.Empty(harness.Manager.GetAllTexts(TextKind.Overview, new() { Source = TestSources.Plugin }));
    }

    #endregion
}

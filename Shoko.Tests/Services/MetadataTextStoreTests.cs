using System;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.Metadata.Text;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers <see cref="MetadataTextStore"/> over an in-memory text cache: the
/// diffs that keep a user's flags, the picks that follow the text they copy,
/// and the writes <see cref="MetadataTextManager"/> takes from any source for
/// any entry. Setting text on a Shoko series drops what the series cached,
/// which it looks up through <c>RepoFactory</c>, so these tests share its
/// collection.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class MetadataTextStoreTests
{
    #region Helpers

    private static readonly MetadataGuid _entity = new(TestSources.AniList, MetadataEntityType.Series, "1");

    private static readonly MetadataGuid _other = new(TestSources.Plugin, MetadataEntityType.Series, "2");

    private static readonly MetadataGuid _shokoSeries = new(MetadataSource.Shoko, MetadataEntityType.Series, "12");

    private static readonly MetadataGuid _shokoEpisode = new(MetadataSource.Shoko, MetadataEntityType.Episode, "34");

    private static TitleStub Title(string value, string languageCode = "en", TitleType type = TitleType.Official, string? countryCode = null)
        => new()
        {
            Source = TestSources.Plugin,
            Value = value,
            Language = TitleLanguage.English,
            LanguageCode = languageCode,
            CountryCode = countryCode,
            Type = type,
        };

    private static TextStub Overview(string value)
        => new() { Source = TestSources.Plugin, Value = value, Language = TitleLanguage.English, LanguageCode = "en" };

    private static (MetadataTextStore Store, TextCache Texts, CacheOnlyRowWriter Writer) TextStore()
    {
        var texts = new TextCache();
        var writer = new CacheOnlyRowWriter();
        return (new MetadataTextStore(texts, writer), texts, writer);
    }

    private static T Shoko<T>(MetadataGuid id) where T : class, IMetadata
    {
        var entry = new Mock<T>();
        entry.SetupGet(e => e.ID).Returns(id);
        return entry.Object;
    }

    private static MetadataTextManager Manager(MetadataTextStore store)
        => new(Mock.Of<IMetadataService>(), store, NullLogger<MetadataTextManager>.Instance);

    #endregion

    #region Store

    [Fact]
    public void ASourcesTitlesAndOverviewsReadBackApartAndInOrder()
    {
        var (store, _, _) = TextStore();

        store.SetTitles(_entity, TestSources.AniList, [Title("Second", type: TitleType.Synonym), Title("First")]);
        store.SetOverviews(_entity, TestSources.AniList, [Overview("About it.")]);
        store.SetTitles(_entity, TestSources.Plugin, [Title("Other")]);

        Assert.Equal(["Second", "First"], store.GetTitles(_entity, TestSources.AniList).Select(title => title.Value));
        Assert.Equal([TitleType.Synonym, TitleType.Official], store.GetTitles(_entity, TestSources.AniList).Select(title => title.Type));
        Assert.All(store.GetTitles(_entity, TestSources.AniList), title => Assert.Equal(TestSources.AniList, title.Source));
        Assert.All(store.GetTitles(_entity), title => Assert.Equal(_entity, title.EntityID));
        Assert.Equal(3, store.GetTitles(_entity).Count);
        Assert.Equal("About it.", Assert.Single(store.GetOverviews(_entity)).Value);
        Assert.Empty(store.GetOverviews(_entity, TestSources.Plugin));
    }

    [Fact]
    public void SettingTitlesAgainKeepsTheRowsThatStayAndReusesTheRowOfAChangedValue()
    {
        var (store, texts, _) = TextStore();
        store.SetTitles(_entity, TestSources.AniList, [Title("A"), Title("B"), Title("C", "ja")]);
        store.SetOverviews(_entity, TestSources.AniList, [Overview("Kept.")]);
        var before = store.GetTitles(_entity).ToDictionary(title => title.Value, title => title.ID);

        store.SetTitles(_entity, TestSources.AniList, [Title("B"), Title("D")]);

        var after = store.GetTitles(_entity);
        Assert.Equal(["B", "D"], after.Select(title => title.Value));
        Assert.Equal(before["B"], after[0].ID);
        Assert.Equal(before["A"], after[1].ID);
        Assert.Equal([0, 1], after.Select(title => title.Ordering));
        Assert.Equal("Kept.", Assert.Single(store.GetOverviews(_entity)).Value);

        store.SetTitles(_entity, TestSources.AniList, []);

        Assert.Empty(store.GetTitles(_entity));
        Assert.Single(texts.GetAll());
    }

    [Fact]
    public void ARefreshKeepsWhatAUserSetOnATextEvenWhenItsValueChanged()
    {
        var (store, _, _) = TextStore();
        var manager = Manager(store);
        store.SetTitles(_entity, TestSources.AniList, [Title("Old"), Title("Kept", "ja")]);
        var old = store.GetTitles(_entity).Single(title => title.Value == "Old");
        var kept = store.GetTitles(_entity).Single(title => title.Value == "Kept");
        manager.SetPreferredTitle(_entity, old);
        manager.EnableText(kept, false);

        store.SetTitles(_entity, TestSources.AniList, [Title("New"), Title("Kept", "ja")]);

        var renamed = store.GetTitles(_entity).Single(title => title.LanguageCode == "en");
        Assert.Equal("New", renamed.Value);
        Assert.Equal(old.ID, renamed.ID);
        Assert.Equal(TextPreference.Overall, renamed.Preference);
        Assert.False(store.GetTitles(_entity).Single(title => title.Value == "Kept").IsEnabled);
    }

    [Fact]
    public void APickFollowsTheTextItCopiesAndGoesWithIt()
    {
        var (store, _, _) = TextStore();
        var manager = Manager(store);
        store.SetTitles(_other, TestSources.Plugin, [Title("Theirs")]);
        var theirs = Assert.Single(store.GetTitles(_other));

        var pick = manager.SetPreferredTitle(_entity, theirs);

        Assert.Equal(MetadataSource.User, pick.Source);
        Assert.Equal(theirs.ID, pick.ReferenceID);
        Assert.Equal(_entity, pick.EntityID);

        store.SetTitles(_other, TestSources.Plugin, [Title("Renamed")]);

        Assert.Equal("Renamed", Assert.Single(store.GetTitles(_entity)).Value);

        store.SetTitles(_other, TestSources.Plugin, []);

        Assert.Empty(store.GetTitles(_entity));
    }

    [Fact]
    public void ABlankLanguageCodeIsStoredAsUnknownAndALongOneIsRefused()
    {
        var (store, _, writer) = TextStore();

        store.SetTitles(_entity, TestSources.AniList, [Title("Name", languageCode: " ", countryCode: " ")]);

        var title = Assert.Single(store.GetTitles(_entity));
        Assert.Equal("unk", title.LanguageCode);
        Assert.Null(title.CountryCode);
        Assert.Throws<ArgumentException>(() => store.SetTitles(_entity, TestSources.AniList, [Title("Ok"), Title("Bad", new string('x', 33))]));
        Assert.Throws<ArgumentException>(() => store.SetTitles(_entity, TestSources.AniList, [Title("Bad", countryCode: new string('x', 33))]));
        Assert.Equal("Name", Assert.Single(store.GetTitles(_entity)).Value);
        Assert.Equal(1, writer.Writes);
    }

    [Fact]
    public void AFailedWriteLeavesTheCacheAsItWas()
    {
        var (store, _, writer) = TextStore();
        store.SetTitles(_entity, TestSources.AniList, [Title("Name")]);
        writer.Fail = true;

        Assert.Throws<InvalidOperationException>(() => store.SetTitles(_entity, TestSources.AniList, [Title("Other")]));
        Assert.Equal("Name", Assert.Single(store.GetTitles(_entity)).Value);
    }

    [Fact]
    public void RemovingAnEntryTakesEverySourcesText()
    {
        var (store, _, _) = TextStore();
        store.SetTitles(_entity, TestSources.AniList, [Title("A")]);
        store.SetTitles(_entity, TestSources.Plugin, [Title("B")]);
        store.SetOverviews(_entity, TestSources.Plugin, [Overview("C")]);

        Assert.Equal(2, store.RemoveEntry(_entity, TestSources.Plugin));
        Assert.Equal(["A"], store.GetTitles(_entity).Select(title => title.Value));
        Assert.Equal(1, store.RemoveEntry(_entity));
        Assert.Empty(store.GetTitles(_entity));
    }

    [Fact]
    public void RemovingASourceTakesOnlyItsTextOnTheEntriesPicked()
    {
        var (store, _, _) = TextStore();
        store.SetTitles(_shokoSeries, TestSources.AniList, [Title("A")]);
        store.SetOverviews(_shokoEpisode, TestSources.AniList, [Overview("B")]);
        store.SetTitles(_shokoSeries, TestSources.Plugin, [Title("C")]);
        store.SetTitles(_entity, TestSources.AniList, [Title("D")]);

        var cleared = store.RemoveSource(TestSources.AniList, entity => entity.Source == MetadataSource.Shoko);

        Assert.Equal([_shokoSeries, _shokoEpisode], cleared.Order());
        Assert.Equal(["C"], store.GetTitles(_shokoSeries).Select(title => title.Value));
        Assert.Empty(store.GetOverviews(_shokoEpisode));
        Assert.Equal(["D"], store.GetTitles(_entity).Select(title => title.Value));
    }

    #endregion

    #region Contributions

    [Fact]
    public void AContributionIsFiledUnderTheEntrysOwnIdentifier()
    {
        var (store, _, _) = TextStore();
        var manager = Manager(store);
        var episode = Shoko<IShokoEpisode>(_shokoEpisode);

        manager.SetTitles(episode, TestSources.AniList, [Title("Contributed")]);
        manager.SetOverviews(episode, TestSources.AniList, [Overview("Said.")]);

        Assert.Equal(["Contributed"], store.GetTitles(_shokoEpisode, TestSources.AniList).Select(title => title.Value));
        Assert.Equal(TestSources.AniList, Assert.Single(manager.GetContributedTitles(episode)).Source);
        Assert.Equal("Said.", Assert.Single(manager.GetContributedOverviews(episode, TestSources.AniList)).Value);
        Assert.Empty(manager.GetContributedTitles(episode, TestSources.Plugin));
    }

    [Fact]
    public void AnySourceMayWriteTextOnAnyEntry()
    {
        var (store, _, _) = TextStore();
        var manager = Manager(store);
        var anime = new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Series, "1");

        manager.SetTitles(anime, TestSources.Plugin, [Title("On AniDB")]);
        manager.SetTitles(Shoko<ISeries>(_entity), MetadataSource.Shoko, [Title("By the core")]);
        manager.SetOverviews(_shokoEpisode, MetadataSource.User, [Overview("By a user")]);

        Assert.Equal("On AniDB", Assert.Single(manager.GetContributedTitles(Shoko<ISeries>(anime))).Value);
        Assert.Equal("By the core", Assert.Single(store.GetTitles(_entity)).Value);
        Assert.Empty(manager.GetContributedOverviews(Shoko<IShokoEpisode>(_shokoEpisode)));
        Assert.Equal("By a user", Assert.Single(store.GetOverviews(_shokoEpisode, MetadataSource.User)).Value);
    }

    [Fact]
    public void RemovingContributionsCountsTheEntriesAndLeavesTheSourcesOwnEntriesAlone()
    {
        var (store, _, _) = TextStore();
        var manager = Manager(store);
        using var scope = new RepoFactoryScope().With<AnimeSeriesRepository, int, AnimeSeries>(series => series.AnimeSeriesID);
        manager.SetTitles(Shoko<IShokoSeries>(_shokoSeries), TestSources.AniList, [Title("A"), Title("B")]);
        manager.SetTitles(Shoko<IShokoEpisode>(_shokoEpisode), TestSources.AniList, [Title("C")]);
        store.SetTitles(_entity, TestSources.AniList, [Title("Own")]);

        Assert.Equal(2, manager.RemoveContributions(TestSources.AniList));
        Assert.Empty(store.GetTitles(_shokoSeries));
        Assert.Equal(["Own"], store.GetTitles(_entity).Select(title => title.Value));
    }

    #endregion
}

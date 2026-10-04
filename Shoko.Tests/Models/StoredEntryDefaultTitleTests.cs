using System.Linq;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Repositories.Cached.Metadata.Text;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Models;

/// <summary>
/// Covers the synthesized default title of a stored entry with no main title of its own: a season's
/// generic name, an episode's generic title, or any other entry's source, kind and ID. It is listed
/// first as the main title, never stored, and a real title in a preferred language still wins.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class StoredEntryDefaultTitleTests
{
    private static RepoFactoryScope ScopeWith(IMetadata entry, out MetadataTextManager manager)
    {
        var service = new Mock<IMetadataService>();
        service.Setup(s => s.GetEntry(entry.ID)).Returns(entry);
        manager = TestTextManager.Build(new MetadataTextStore(new TextCache(), new CacheOnlyRowWriter()), service.Object);
        return new RepoFactoryScope().Set(manager);
    }

    private static Metadata_Season Season(int number)
        => new() { Source = TestSources.Plugin, ProviderID = $"s{number}", SeriesID = "1", SeasonNumber = number };

    private static TitleStub Title(string value, TitleLanguage language, string code, TitleType type)
        => new() { Source = TestSources.Plugin, Value = value, Language = language, LanguageCode = code, Type = type };

    [Theory]
    [InlineData(2, "Season 2")]
    [InlineData(0, "Specials")]
    public void ASeasonWithNoTitlesIsCalledByItsGenericName(int number, string expected)
    {
        var season = Season(number);
        using var scope = ScopeWith(season, out var manager);
        var titled = (IWithTitles)season;

        Assert.True(Assert.Single(titled.Titles).IsSynthesized);
        Assert.Equal(expected, titled.Title);
        Assert.True(titled.PreferredTitle?.IsSynthesized);
        Assert.Equal(expected, titled.DefaultTitle.Value);
        Assert.True(titled.DefaultTitle.IsSynthesized);
        var byID = manager.GetPreferredTitle(((IMetadata)season).ID);
        Assert.Equal(expected, byID?.Value);
        Assert.True(byID?.IsSynthesized);
    }

    [Fact]
    public void ASeasonWithOnlyATitleThatIsNotItsMainOneListsItsGenericNameFirstAsTheDefault()
    {
        var season = Season(2);
        using var scope = ScopeWith(season, out var manager);
        manager.SetTitles(((IMetadata)season).ID, TestSources.Plugin, [Title("第2期", TitleLanguage.Japanese, "ja", TitleType.Official)]);
        var titled = (IWithTitles)season;

        Assert.Equal(["Season 2", "第2期"], titled.Titles.Select(title => title.Value));
        Assert.Equal(TitleType.Main, titled.Titles[0].Type);
        Assert.True(titled.DefaultTitle.IsSynthesized);
        Assert.Equal("Season 2", manager.GetDefaultTitle(((IMetadata)season).ID)?.Value);
        Assert.Equal("Season 2", titled.Title);

        // Nothing synthesized is stored.
        Assert.Equal(["第2期"], manager.GetTitles(((IMetadata)season).ID).Select(title => title.Value));
    }

    [Fact]
    public void ASeasonsGenericMainTitleIsDroppedAndNoOtherTitleIsPromoted()
    {
        var season = Season(2);
        using var scope = ScopeWith(season, out var manager);
        manager.SetTitles(((IMetadata)season).ID, TestSources.Plugin, [
            Title("Season 2", TitleLanguage.English, "en", TitleType.Main),
            Title("La búsqueda", TitleLanguage.Spanish, "es", TitleType.Official),
        ]);
        var titled = (IWithTitles)season;

        Assert.Equal(["La búsqueda"], manager.GetTitles(((IMetadata)season).ID).Select(title => title.Value));
        Assert.Equal([("Season 2", TitleType.Main), ("La búsqueda", TitleType.Official)], titled.Titles.Select(title => (title.Value, title.Type)));
        Assert.Equal("Season 2", titled.DefaultTitle.Value);
        Assert.True(titled.DefaultTitle.IsSynthesized);
    }

    [Fact]
    public void ASeasonsMainTitleKeepsTheGenericNameOut()
    {
        var season = Season(2);
        using var scope = ScopeWith(season, out var manager);
        manager.SetTitles(((IMetadata)season).ID, TestSources.Plugin, [Title("Staffel", TitleLanguage.German, "de", TitleType.Main)]);
        var titled = (IWithTitles)season;

        Assert.Equal(["Staffel"], titled.Titles.Select(title => title.Value));
        Assert.Equal("Staffel", titled.Title);
        Assert.False(titled.DefaultTitle.IsSynthesized);
    }

    [Fact]
    public void AnEpisodesRealTitleInAPreferredLanguageBeatsItsSynthesizedDefault()
    {
        // Episodes are named in English in the stub settings.
        var episode = new Metadata_Episode { Source = TestSources.Plugin, ProviderID = "e5", SeriesID = "1", EpisodeNumber = 5 };
        using var scope = ScopeWith(episode, out var manager);
        manager.SetTitles(((IMetadata)episode).ID, TestSources.Plugin, [Title("The Real One", TitleLanguage.English, "en", TitleType.Official)]);
        var titled = (IWithTitles)episode;

        Assert.Equal(["Episode 5", "The Real One"], titled.Titles.Select(title => title.Value));
        Assert.True(titled.DefaultTitle.IsSynthesized);
        Assert.Equal("The Real One", titled.Title);
        Assert.Equal("The Real One", manager.GetPreferredTitle(((IMetadata)episode).ID)?.Value);
    }

    [Fact]
    public void AnyOtherEntryWithoutAMainTitleIsCalledByItsSourceKindAndID()
    {
        var series = new Metadata_Series { Source = TestSources.Plugin, ProviderID = "46195" };
        using var scope = ScopeWith(series, out _);
        var titled = (IWithTitles)series;

        var synthesized = Assert.Single(titled.Titles);
        Assert.Equal("TestPlugin Series 46195", synthesized.Value);
        Assert.Equal(TitleLanguage.Unknown, synthesized.Language);
        Assert.True(synthesized.IsSynthesized);
        Assert.Equal("TestPlugin Series 46195", titled.DefaultTitle.Value);
    }

    [Fact]
    public void AUsersPickWinsOverTheSynthesizedDefault()
    {
        var series = new Metadata_Series { Source = TestSources.Plugin, ProviderID = "7" };
        using var scope = ScopeWith(series, out var manager);
        manager.SetTitles(((IMetadata)series).ID, TestSources.Plugin, [Title("Serien", TitleLanguage.Swedish, "sv", TitleType.Official)]);
        var titled = (IWithTitles)series;
        Assert.Equal("TestPlugin Series 7", titled.Title);

        manager.SetPreferredTitle(((IMetadata)series).ID, titled.Titles[1]);

        Assert.Equal("Serien", titled.Title);
        Assert.True(titled.DefaultTitle.IsSynthesized);
    }
}

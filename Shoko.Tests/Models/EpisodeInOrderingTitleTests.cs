using System.Linq;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Models.Metadata.Embedded;
using Shoko.Server.Repositories.Cached.Metadata.Text;
using Shoko.Server.Services;
using Shoko.Server.Utilities;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Models;

/// <summary>
/// Covers the titles of an episode as an ordering presents it: an episode with no main title gets
/// synthesized titles in the ordering's numbering, while its real titles and a user's pick still win.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class EpisodeInOrderingTitleTests
{
    private static RepoFactoryScope ScopeWith(IMetadata entry, out MetadataTextManager manager)
    {
        var service = new Mock<IMetadataService>();
        service.Setup(s => s.GetEntry(entry.ID)).Returns(entry);
        manager = TestTextManager.Build(new MetadataTextStore(new TextCache(), new CacheOnlyRowWriter()), service.Object);
        return new RepoFactoryScope().Set(manager);
    }

    private static Metadata_Episode Episode()
        => new() { Source = TestSources.Plugin, ProviderID = "e5", SeriesID = "1", EpisodeNumber = 5, Type = EpisodeType.Episode };

    private static IEpisode Placed(IEpisode episode, int number, bool special = false)
    {
        var group = new Mock<ISeason>();
        group.SetupGet(season => season.SeasonNumber).Returns(special ? 0 : 1);
        group.SetupGet(season => season.IsSpecial).Returns(special);
        IEpisodeOrderingInformation place = new StoredEpisodeOrdering(Mock.Of<IOrdering>(), group.Object, episode, number, null);
        return place.Episode;
    }

    private static TitleStub Title(string value, TitleLanguage language, string code)
        => new() { Source = TestSources.Plugin, Value = value, Language = language, LanguageCode = code, Type = TitleType.Official };

    [Theory]
    [InlineData(false, 7, EpisodeType.Episode, "Episode 7")]
    [InlineData(true, 2, EpisodeType.Special, "Episode 2")]
    public void AnUntitledEpisodeIsNamedByItsPlaceInTheOrdering(bool special, int number, EpisodeType type, string expected)
    {
        var episode = Episode();
        using var scope = ScopeWith(episode, out _);
        var placed = Placed(episode, number, special);
        var languages = Languages.PreferredEpisodeNamingLanguages.Select(language => language.Language).Where(language => language is not TitleLanguage.Main);

        Assert.Equal(expected, placed.Title);
        Assert.True(placed.DefaultTitle.IsSynthesized);
        Assert.Equal(
            GenericEpisodeTitles.SynthesizeAll(type, number, false, languages).Select(title => title.Value),
            placed.Titles.Select(title => title.Value)
        );

        // The episode itself, and its place in the default ordering, keep its own number.
        Assert.Equal("Episode 5", ((IWithTitles)episode).Title);
        Assert.Equal("Episode 5", ((IEpisodeOrderingInformation)new DefaultEpisodeOrdering(episode, null, null)).Episode.Title);
    }

    [Fact]
    public void ATmdbSeasonZeroEpisodeWithTheSpecialTypeSynthesizesThePlainEpisodeForm()
    {
        var episode = new Metadata_Episode
        {
            Source = MetadataSource.TMDB,
            ProviderID = "e3",
            SeriesID = "1",
            SeasonNumber = 0,
            EpisodeNumber = 3,
            Type = EpisodeType.Special,
        };
        using var scope = ScopeWith(episode, out _);

        Assert.Equal("Episode 3", ((IWithTitles)episode).Title);
        Assert.True(((IWithTitles)episode).DefaultTitle!.IsSynthesized);
    }

    [Fact]
    public void ARealTitleStillWinsInAnOrdering()
    {
        var episode = Episode();
        using var scope = ScopeWith(episode, out var manager);
        manager.SetTitles(((IMetadata)episode).ID, TestSources.Plugin, [Title("The Real One", TitleLanguage.English, "en")]);
        var placed = Placed(episode, 7);

        Assert.Equal("The Real One", placed.Title);
        Assert.Equal(["Episode 7", "The Real One"], placed.Titles.Select(title => title.Value));
    }

    [Fact]
    public void AUsersPickWinsInEveryOrdering()
    {
        var episode = Episode();
        using var scope = ScopeWith(episode, out var manager);
        manager.SetTitles(((IMetadata)episode).ID, TestSources.Plugin, [Title("第五話の題", TitleLanguage.Japanese, "ja")]);
        var placed = Placed(episode, 7);
        Assert.Equal("Episode 7", placed.Title);

        manager.SetPreferredTitle(((IMetadata)episode).ID, ((IWithTitles)episode).Titles[1]);

        Assert.Equal("第五話の題", placed.Title);
        Assert.Equal("第五話の題", ((IWithTitles)episode).Title);
    }

    [Theory]
    [InlineData(false, 7, "Episode 7")]
    [InlineData(true, 2, "Episode S2")]
    public void AnAnidbEpisodesGenericTitleFollowsItsPlace(bool special, int number, string expected)
    {
        var episode = new AniDB_Episode { EpisodeID = 50, AnimeID = 1, EpisodeNumber = 5, EpisodeType = EpisodeType.Episode };
        using var scope = ScopeWith(episode, out _);

        Assert.Equal(expected, Placed(episode, number, special).Title);
        Assert.Equal("Episode 5", episode.Title);
    }
}

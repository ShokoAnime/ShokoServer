using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers the parts of <see cref="MetadataTextManager"/>'s walks that decide
/// which of one source's titles answers for a language, and which linked film
/// or series speaks for an episode or a whole anime.
/// </summary>
public class MetadataTextManagerTests
{
    private static TitleStub Title(string value, TitleLanguage language, string code, TitleType type = TitleType.Official, string? country = null)
        => new() { Source = MetadataSource.TMDB, Value = value, Language = language, LanguageCode = code, CountryCode = country, Type = type };

    [Fact]
    public void ATextSpeaksALanguageByItsCodesAsWellAsItsLanguage()
    {
        // A title whose language was left unknown still speaks by its code.
        Assert.True(MetadataTextManager.Speaks(Title("x", TitleLanguage.Unknown, "en"), TitleLanguage.English));
        Assert.True(MetadataTextManager.Speaks(Title("x", TitleLanguage.English, "unk"), TitleLanguage.English));
        Assert.False(MetadataTextManager.Speaks(Title("x", TitleLanguage.Japanese, "ja"), TitleLanguage.English));
    }

    [Fact]
    public void AnOfficialTitleIsPickedOverASynonymInTheSameLanguage()
    {
        IReadOnlyList<ITitle> titles =
        [
            Title("Synonym", TitleLanguage.English, "en", TitleType.Synonym),
            Title("Official", TitleLanguage.English, "en"),
        ];

        Assert.Equal("Official", MetadataTextManager.Pick(titles, TitleLanguage.English)?.Value);
    }

    [Fact]
    public void ASynonymIsStillPickedWhenItIsAllTheSourceHas()
    {
        IReadOnlyList<ITitle> titles = [Title("Synonym", TitleLanguage.English, "en", TitleType.Synonym)];

        Assert.Equal("Synonym", MetadataTextManager.Pick(titles, TitleLanguage.English)?.Value);
    }

    [Fact]
    public void XMainIsNeverPickedFromASourceOtherThanAniDB()
    {
        IReadOnlyList<ITitle> titles = [Title("Main", TitleLanguage.Main, "x-main", TitleType.Main)];

        Assert.Null(MetadataTextManager.Pick(titles, TitleLanguage.Main));
        Assert.Empty(MetadataTextManager.WithoutMain(titles));
    }

    #region Only Entry

    private static IMetadataCrossReference Link(string id, bool stored)
    {
        var guid = new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Movie, id);
        var entry = new Mock<IMetadata>();
        entry.SetupGet(e => e.ID).Returns(guid);
        var link = new Mock<IMetadataCrossReference>();
        link.SetupGet(l => l.ProviderID).Returns(guid);
        link.SetupGet(l => l.Source).Returns(MetadataSource.TMDB);
        link.SetupGet(l => l.Provider).Returns(stored ? entry.Object : null);
        return link.Object;
    }

    [Theory]
    [InlineData(new[] { "1112867", "1106304" }, new[] { false, true }, "1106304")]
    [InlineData(new[] { "81", "81" }, new[] { true, true }, "81")]
    [InlineData(new[] { "426518", "552490" }, new[] { true, true }, null)]
    [InlineData(new[] { "993162" }, new[] { false }, null)]
    public void OnlyTheOneStoredFilmTheLinksName_Speaks(string[] ids, bool[] stored, string? expected)
        => Assert.Equal(expected, MetadataTextManager.OnlyEntry([.. ids.Select((id, index) => Link(id, stored[index]))])?.ID.ID);

    #endregion

    #region Linked Films

    private const int AnimeID = 66;

    private static readonly MetadataGuid _film = new(TestSources.Plugin, MetadataEntityType.Movie, "15999");

    private static readonly MetadataGuid _otherFilm = new(TestSources.Plugin, MetadataEntityType.Movie, "16000");

    private static readonly MetadataGuid _collection = new(TestSources.Plugin, MetadataEntityType.Collection, "7");

    private static T Entry<T>(MetadataGuid id) where T : class, IMetadata
    {
        var entry = new Mock<T>();
        entry.SetupGet(e => e.ID).Returns(id);
        entry.SetupGet(e => e.Source).Returns(id.Source);
        return entry.Object;
    }

    private static IMetadataMovieCrossReference FilmLink(int anidbEpisodeID, MetadataGuid film, bool stored = true)
    {
        var link = new Mock<IMetadataMovieCrossReference>();
        link.SetupGet(l => l.AnidbAnimeID).Returns(AnimeID);
        link.SetupGet(l => l.AnidbEpisodeID).Returns(anidbEpisodeID);
        link.SetupGet(l => l.ProviderID).Returns(film);
        link.SetupGet(l => l.Source).Returns(film.Source);
        link.SetupGet(l => l.Provider).Returns(stored ? Entry<IMovie>(film) : null);
        return link.Object;
    }

    private static MetadataTextManager Manager(IReadOnlyList<IMetadataMovieCrossReference> links, IReadOnlyList<IMetadataEpisodeCrossReference>? episodeLinks = null,
        IReadOnlyList<ICollection>? collections = null, IReadOnlyList<IMetadataSeriesCrossReference>? seriesLinks = null,
        IReadOnlyList<IMetadataSeasonCrossReference>? seasonLinks = null)
    {
        var service = new Mock<IMetadataService>();
        service.Setup(s => s.GetSeriesCrossReferences(AnimeID, TestSources.Plugin)).Returns(seriesLinks ?? []);
        service.Setup(s => s.GetSeasonCrossReferences(AnimeID, TestSources.Plugin)).Returns(seasonLinks ?? []);
        service.Setup(s => s.GetEpisodeCrossReferencesForSeries(AnimeID, TestSources.Plugin)).Returns(episodeLinks ?? []);
        service.Setup(s => s.GetMovieCrossReferencesForSeries(AnimeID, TestSources.Plugin)).Returns(links);
        service.Setup(s => s.GetMovieCrossReferences(It.IsAny<int>(), TestSources.Plugin))
            .Returns((int episodeID, MetadataSource? _) => [.. links.Where(link => link.AnidbEpisodeID == episodeID)]);
        service.Setup(s => s.GetEpisodeCrossReferences(It.IsAny<int>(), TestSources.Plugin)).Returns(episodeLinks ?? []);
        service.Setup(s => s.GetCollectionsWith(It.IsAny<MetadataGuid>())).Returns(collections ?? []);
        return new(service.Object, null!, NullLogger<MetadataTextManager>.Instance);
    }

    [Fact]
    public void AFilmOnlyOneEpisodeIsLinkedTo_SpeaksForIt()
    {
        var (entry, placeholder) = Manager([FilmLink(1, _film)]).EpisodeEntry(AnimeID, 1, "Complete Movie", TestSources.Plugin);

        Assert.Equal(_film, entry?.ID);
        Assert.Null(placeholder);
    }

    [Fact]
    public void AnEpisodeLink_WinsOverAFilm()
    {
        var episodeID = new MetadataGuid(TestSources.Plugin, MetadataEntityType.Episode, "5");
        var episodeLink = new Mock<IMetadataEpisodeCrossReference>();
        episodeLink.SetupGet(l => l.ProviderID).Returns(episodeID);
        episodeLink.SetupGet(l => l.Provider).Returns(Entry<IEpisode>(episodeID));

        var (entry, placeholder) = Manager([FilmLink(1, _film), FilmLink(2, _film)], [episodeLink.Object]).EpisodeEntry(AnimeID, 1, "Part 1 of 2", TestSources.Plugin);

        Assert.Equal(episodeID, entry?.ID);
        Assert.Null(placeholder);
    }

    [Theory]
    [InlineData("Part 1 of 2", "Vampire Hunter D (Part 1 of 2)")]
    [InlineData("Complete Movie", "Vampire Hunter D")]
    [InlineData("Episode 2", "Vampire Hunter D (Episode 2)")]
    public void ASharedFilm_SpeaksForAStandInWithItsLabel(string anidbTitle, string expected)
    {
        var (entry, placeholder) = Manager([FilmLink(1, _film), FilmLink(2, _film), FilmLink(3, _film)]).EpisodeEntry(AnimeID, 2, anidbTitle, TestSources.Plugin);

        Assert.Equal(_film, entry?.ID);
        var titles = MetadataTextManager.Labelled([Title("Vampire Hunter D", TitleLanguage.English, "en")], placeholder);
        Assert.Equal(expected, titles.Single().Value);
        Assert.Equal(TitleLanguage.English, titles.Single().Language);
    }

    [Theory]
    [InlineData("Magnetic Rose")]
    [InlineData("Death")]
    [InlineData(null)]
    public void ASharedFilm_LeavesANamedEpisodeToAniDB(string? anidbTitle)
    {
        var (entry, placeholder) = Manager([FilmLink(1, _film), FilmLink(2, _film)]).EpisodeEntry(AnimeID, 1, anidbTitle, TestSources.Plugin);

        Assert.Null(entry);
        Assert.Null(placeholder);
    }

    [Fact]
    public void AFilmOfAnotherEpisode_DoesNotMakeAFilmShared()
    {
        var (entry, placeholder) = Manager([FilmLink(1, _film), FilmLink(2, _otherFilm)]).EpisodeEntry(AnimeID, 1, "Magnetic Rose", TestSources.Plugin);

        Assert.Equal(_film, entry?.ID);
        Assert.Null(placeholder);
    }

    [Fact]
    public void TheWholeAnime_ReadsTheCollectionItsFilmsShare()
    {
        var entry = Manager([FilmLink(1, _film), FilmLink(2, _otherFilm)], collections: [Entry<ICollection>(_collection)])
            .FilmEntry(AnimeID, TestSources.Plugin, [(1, EpisodeType.Episode, "First"), (2, EpisodeType.Episode, "Second")]);

        Assert.Equal(_collection, entry?.ID);
    }

    [Fact]
    public void AFilmThatIsNotStored_LeavesItsEpisodeUnlinked()
    {
        var entry = Manager([FilmLink(1, _film), FilmLink(2, _film, stored: false)])
            .FilmEntry(AnimeID, TestSources.Plugin, [(1, EpisodeType.Episode, "Episode 1"), (2, EpisodeType.Episode, "Episode 2")]);

        Assert.Null(entry);
    }

    #endregion

    #region Series Side

    private static readonly MetadataGuid _show = new(TestSources.Plugin, MetadataEntityType.Series, "81");

    private static readonly MetadataGuid _season = new(TestSources.Plugin, MetadataEntityType.Season, "81-2");

    private static IMetadataSeriesCrossReference ShowLink()
    {
        var link = new Mock<IMetadataSeriesCrossReference>();
        link.SetupGet(l => l.ProviderID).Returns(_show);
        link.SetupGet(l => l.Source).Returns(_show.Source);
        link.SetupGet(l => l.Provider).Returns(Entry<ISeries>(_show));
        return link.Object;
    }

    private static IMetadataSeasonCrossReference SeasonLink(int seasonNumber)
    {
        var link = new Mock<IMetadataSeasonCrossReference>();
        link.SetupGet(l => l.ProviderID).Returns(_season);
        link.SetupGet(l => l.Source).Returns(_season.Source);
        link.SetupGet(l => l.SeasonNumber).Returns(seasonNumber);
        link.SetupGet(l => l.Provider).Returns(Entry<ISeason>(_season));
        return link.Object;
    }

    private static IEnumerable<(int ID, EpisodeType Type, string? Title)> OneFilmEpisode()
        => [(1, EpisodeType.Episode, "Complete Movie")];

    private static (MetadataGuid? Title, MetadataGuid? Description) SeriesEntries(MetadataTextManager manager, AnimeType animeType)
        => (manager.SeriesTitleEntry(AnimeID, animeType, TestSources.Plugin, OneFilmEpisode)?.ID,
            manager.SeriesDescriptionEntry(AnimeID, animeType, TestSources.Plugin, OneFilmEpisode)?.ID);

    [Theory]
    [InlineData(AnimeType.Movie)]
    [InlineData(AnimeType.Web)]
    [InlineData(AnimeType.TVSpecial)]
    public void AMovieWithBothSides_ReadsItsFilm(AnimeType animeType)
    {
        var manager = Manager([FilmLink(1, _film)], seriesLinks: [ShowLink()], seasonLinks: [SeasonLink(1)]);

        Assert.Equal((_film, _film), SeriesEntries(manager, animeType));
    }

    [Theory]
    [InlineData(AnimeType.OVA)]
    [InlineData(AnimeType.TV)]
    [InlineData(AnimeType.Other)]
    public void AnOvaWithBothSides_ReadsItsSeries(AnimeType animeType)
    {
        var manager = Manager([FilmLink(1, _film)], seriesLinks: [ShowLink()], seasonLinks: [SeasonLink(1)]);

        Assert.Equal((_show, _show), SeriesEntries(manager, animeType));
    }

    [Fact]
    public void AMovieWithoutFilms_FallsToItsSeries()
    {
        var manager = Manager([], seriesLinks: [ShowLink()], seasonLinks: [SeasonLink(2)]);

        Assert.Equal((_show, _season), SeriesEntries(manager, AnimeType.Movie));
    }

    [Fact]
    public void AMovieWhoseFilmsSayNothing_FallsToItsSeries()
    {
        var manager = Manager([FilmLink(2, _film)], seriesLinks: [ShowLink()], seasonLinks: [SeasonLink(1)]);

        Assert.Equal((_show, _show), SeriesEntries(manager, AnimeType.Movie));
    }

    [Fact]
    public void AnOvaWithoutASeries_FallsToItsFilm()
        => Assert.Equal((_film, _film), SeriesEntries(Manager([FilmLink(1, _film)]), AnimeType.OVA));

    [Theory]
    [InlineData(AnimeType.Movie)]
    [InlineData(AnimeType.OVA)]
    public void NeitherSide_SaysNothing(AnimeType animeType)
        => Assert.Equal((null, null), SeriesEntries(Manager([FilmLink(2, _film)]), animeType));

    [Fact]
    public void ASeriesWithNoLinkedEpisodes_GivesItsTitlesButNoFilmDescription()
    {
        var manager = Manager([FilmLink(1, _film)], seriesLinks: [ShowLink()]);

        Assert.Equal((_show, null), SeriesEntries(manager, AnimeType.OVA));
    }

    [Fact]
    public void AMovieWithAFilm_IgnoresASeriesWithNoLinkedEpisodes()
    {
        var manager = Manager([FilmLink(1, _film)], seriesLinks: [ShowLink()]);

        Assert.Equal((_film, _film), SeriesEntries(manager, AnimeType.Movie));
    }

    #endregion
}

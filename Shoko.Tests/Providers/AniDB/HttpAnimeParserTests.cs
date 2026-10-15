using System;
using Microsoft.Extensions.Logging.Abstractions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Providers.AniDB.HTTP;
using Xunit;

using AnimeType = Shoko.Server.Providers.AniDB.AnimeType;

namespace Shoko.Tests.Providers.AniDB;

/// <summary>
/// Covers <see cref="HttpAnimeParser"/>, which turns AniDB's anime XML into the records the rest of
/// the server is built on. It is pure — XML in, objects out — and had no tests, despite AniDB being
/// one of the most frequently fixed areas in the codebase.
/// </summary>
public class HttpAnimeParserTests
{
    private static HttpAnimeParser Parser() => new(NullLogger<HttpAnimeParser>.Instance);

    /// <summary>Builds an anime document with sensible defaults, overriding the parts under test.</summary>
    private static string Xml(
        string id = "1",
        string type = "TV Series",
        string episodeCount = "12",
        string startDate = "2020-01-05",
        string endDate = "2020-03-22",
        string? restricted = "false",
        string titles = "<title xml:lang=\"x-jat\" type=\"main\">Main Title</title>",
        string description = "A description.",
        string extra = "")
        => $"""
            <anime id="{id}" restricted="{restricted}">
              <type>{type}</type>
              <episodecount>{episodeCount}</episodecount>
              <startdate>{startDate}</startdate>
              <enddate>{endDate}</enddate>
              <url>https://example.invalid/show</url>
              <picture>1234.jpg</picture>
              <description>{description}</description>
              <titles>{titles}</titles>
              {extra}
            </anime>
            """;

    private static ResponseGetAnime ParseOrFail(string xml)
        => Parser().Parse(1, xml) ?? throw new InvalidOperationException("Parse returned null.");

    #region Rejecting unusable documents

    [Fact]
    public void ADocumentWithoutAnAnimeIdIsRejected()
        => Assert.Null(Parser().Parse(1, "<anime><titles><title type=\"main\">X</title></titles></anime>"));

    [Theory]
    [InlineData("<title xml:lang=\"en\" type=\"official\">Official Only</title>")] // No main title at all.
    [InlineData("<title xml:lang=\"x-jat\" type=\"main\">   </title>")] // A blank main title counts as missing.
    public void ADocumentWithoutAUsableMainTitleIsRejected(string titles)
    {
        // Everything downstream keys off the main title, so a document lacking one is unusable.
        Assert.Null(Parser().Parse(1, Xml(titles: titles)));
    }

    #endregion

    #region Anime details

    [Fact]
    public void TheAnimeIdComesFromTheCallerNotTheDocument()
    {
        var response = Parser().Parse(99, Xml(id: "1"))!;

        Assert.Equal(99, response.Anime.AnimeID);
    }

    [Theory]
    [InlineData("Movie", AnimeType.Movie)]
    [InlineData("OVA", AnimeType.OVA)]
    [InlineData("TV Series", AnimeType.TVSeries)]
    [InlineData("TV Special", AnimeType.TVSpecial)]
    [InlineData("Web", AnimeType.Web)]
    [InlineData("Music Video", AnimeType.MusicVideo)]
    [InlineData("Other", AnimeType.Other)]
    [InlineData("tv series", AnimeType.TVSeries)] // Matched without regard to case.
    [InlineData("Interpretive Dance", AnimeType.Unknown)] // An unrecognised type becomes Unknown.
    public void TheAnimeTypeIsMapped(string type, AnimeType expected)
        => Assert.Equal(expected, ParseOrFail(Xml(type: type)).Anime.AnimeType);

    [Theory]
    [InlineData("12", 12)]
    [InlineData("lots", 0)] // An unreadable count becomes zero.
    public void TheEpisodeCountIsRead(string episodeCount, int expected)
        => Assert.Equal(expected, ParseOrFail(Xml(episodeCount: episodeCount)).Anime.EpisodeCount);

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("nonsense", false)]
    public void TheRestrictedFlagIsRead(string restricted, bool expected)
        => Assert.Equal(expected, ParseOrFail(Xml(restricted: restricted)).Anime.IsRestricted);

    [Fact]
    public void BackticksInTheDescriptionBecomeApostrophes()
    {
        // AniDB writes apostrophes as backticks throughout its API.
        var response = ParseOrFail(Xml(description: "It`s a description."));

        Assert.Equal("It's a description.", response.Anime.Description);
    }

    #endregion

    #region Dates

    [Fact]
    public void TheAirAndEndDatesAreRead()
    {
        var anime = ParseOrFail(Xml(startDate: "2020-01-05", endDate: "2020-03-22")).Anime;

        Assert.Equal(new PartialDateOnly(2020, 1, 5), anime.AirDate);
        Assert.Equal(new PartialDateOnly(2020, 3, 22), anime.EndDate);
        Assert.Equal(2020, anime.BeginYear);
        Assert.Equal(2020, anime.EndYear);
    }

    [Fact]
    public void TheUnixEpochIsTreatedAsNoDate()
    {
        // AniDB uses 1970-01-01 as its "unknown" sentinel; taking it literally would place shows in
        // 1970 and break every year filter and season grouping.
        var anime = ParseOrFail(Xml(startDate: "1970-01-01", endDate: "1970-01-01")).Anime;

        Assert.Null(anime.AirDate);
        Assert.Null(anime.EndDate);
        Assert.Equal(0, anime.BeginYear);
        Assert.Equal(0, anime.EndYear);
    }

    [Fact]
    public void AYearOnlyDateIsKeptPartial()
    {
        var anime = ParseOrFail(Xml(startDate: "2020", endDate: "")).Anime;

        Assert.Equal(2020, anime.AirDate!.Value.Year);
        Assert.Null(anime.AirDate.Value.Month);
        Assert.Null(anime.EndDate);
    }

    [Fact]
    public void AStillAiringShowHasNoEndDate()
    {
        var anime = ParseOrFail(Xml(endDate: "")).Anime;

        Assert.NotNull(anime.AirDate);
        Assert.Null(anime.EndDate);
        Assert.Equal(0, anime.EndYear);
    }

    #endregion

    #region Titles

    [Fact]
    public void TitlesAreReadWithTheirTypeAndLanguage()
    {
        var response = ParseOrFail(Xml(titles: """
            <title xml:lang="x-jat" type="main">Romaji Title</title>
            <title xml:lang="en" type="official">English Title</title>
            <title xml:lang="ja" type="synonym">Japanese Synonym</title>
            """));

        Assert.Equal(3, response.Titles.Count);
        var main = Assert.Single(response.Titles, t => t.TitleType == TitleType.Main);
        Assert.Equal("Romaji Title", main.Title);
        Assert.Equal(TitleLanguage.Romaji, main.Language);
        Assert.Equal(TitleLanguage.English, Assert.Single(response.Titles, t => t.TitleType == TitleType.Official).Language);
        Assert.Equal(TitleLanguage.Japanese, Assert.Single(response.Titles, t => t.TitleType == TitleType.Synonym).Language);
    }

    [Fact]
    public void BackticksInTitlesBecomeApostrophes()
    {
        var response = ParseOrFail(Xml(titles: "<title xml:lang=\"en\" type=\"main\">It`s Here</title>"));

        Assert.Equal("It's Here", response.Anime.MainTitle);
    }

    #endregion

    #region Episodes

    private static string EpisodeXml(string epno, string id = "1001", string extra = "", string? airDate = "2020-01-05")
        => $"""
            <episodes>
              <episode id="{id}" update="2020-01-05">
                <epno>{epno}</epno>
                <length>24</length>
                {(airDate is null ? string.Empty : $"<airdate>{airDate}</airdate>")}
                <title xml:lang="en">Episode Title</title>
                {extra}
              </episode>
            </episodes>
            """;

    [Theory]
    [InlineData("1", 1, EpisodeType.Episode)]
    [InlineData("12", 12, EpisodeType.Episode)]
    [InlineData("S1", 1, EpisodeType.Special)]
    [InlineData("C2", 2, EpisodeType.Credits)]
    [InlineData("T3", 3, EpisodeType.Trailer)]
    [InlineData("P4", 4, EpisodeType.Parody)]
    [InlineData("O5", 5, EpisodeType.Other)]
    [InlineData("1-2", 1, EpisodeType.Episode)] // AniDB writes a combined release as "1-2"; the first number is used.
    public void TheEpisodeNumberAndTypeAreReadFromTheEpno(string epno, int expectedNumber, EpisodeType expectedType)
    {
        var episode = Assert.Single(ParseOrFail(Xml(extra: EpisodeXml(epno))).Episodes);

        Assert.Equal(expectedNumber, episode.EpisodeNumber);
        Assert.Equal(expectedType, episode.EpisodeType);
    }

    [Fact]
    public void TheEpisodeLengthIsConvertedFromMinutesToSeconds()
        => Assert.Equal(24 * 60, Assert.Single(ParseOrFail(Xml(extra: EpisodeXml("1"))).Episodes).LengthSeconds);

    [Fact]
    public void TheEpisodeIdAndAnimeIdAreRecorded()
    {
        var episode = Assert.Single(Parser().Parse(77, Xml(extra: EpisodeXml("1", id: "5150")))!.Episodes);

        Assert.Equal(5150, episode.EpisodeID);
        Assert.Equal(77, episode.AnimeID);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("1970-01-01", 0)]
    [InlineData("2020-01-05", 1578182400)]
    public void TheEpisodeAirDateKeepsAMissingDateApartFromThePlaceholder(string? airDate, int? expected)
    {
        var episode = Assert.Single(ParseOrFail(Xml(extra: EpisodeXml("1", airDate: airDate))).Episodes);

        Assert.Equal(expected, AnimeCreator.NewEpisode(episode, DateTime.Now).AirDate);
    }

    [Fact]
    public void AMissingUpdateDateFallsBackToTheUnixEpoch()
    {
        var xml = Xml(extra: """
            <episodes>
              <episode id="1001">
                <epno>1</epno>
                <length>24</length>
              </episode>
            </episodes>
            """);

        Assert.Equal(DateTime.UnixEpoch, Assert.Single(ParseOrFail(xml).Episodes).LastUpdated);
    }

    [Fact]
    public void EpisodeTitlesInAnUnknownLanguageAreDropped()
    {
        var xml = Xml(extra: EpisodeXml("1", extra: "<title xml:lang=\"zz-nonsense\">Unusable</title>"));

        var episode = Assert.Single(ParseOrFail(xml).Episodes);
        Assert.All(episode.Titles, title => Assert.NotEqual(TitleLanguage.Unknown, title.Language));
        Assert.Equal("Episode Title", Assert.Single(episode.Titles).Title);
    }

    #endregion

    #region Related collections

    [Fact]
    public void RelationsAreRead()
    {
        var xml = Xml(extra: """
            <relatedanime>
              <anime id="200" type="Sequel">Next Season</anime>
            </relatedanime>
            """);

        var relation = Assert.Single(ParseOrFail(xml).Relations);
        Assert.Equal(200, relation.RelatedAnimeID);
    }

    [Fact]
    public void SimilarAnimeAreRead()
    {
        var xml = Xml(extra: """
            <similaranime>
              <anime id="300" approval="10" total="12">Something Alike</anime>
            </similaranime>
            """);

        var similar = Assert.Single(ParseOrFail(xml).Similar);
        Assert.Equal(300, similar.SimilarAnimeID);
    }

    [Fact]
    public void MissingCollectionsComeBackEmptyRatherThanNull()
    {
        var response = ParseOrFail(Xml());

        Assert.Empty(response.Episodes);
        Assert.Empty(response.Tags);
        Assert.Empty(response.Characters);
        Assert.Empty(response.Staff);
        Assert.Empty(response.Relations);
        Assert.Empty(response.Similar);
        Assert.Empty(response.Resources);
    }

    #endregion
}

using Microsoft.Extensions.Logging.Abstractions;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Providers.AniDB.HTTP;
using Xunit;

namespace Shoko.Tests.Databases;

/// <summary>
/// The data fix putting AniDB's 1970-01-01 placeholder back on the episodes the
/// schema steps left undated, from each anime's cached XML.
/// </summary>
public class AnidbPlaceholderAirDateFixupTests
{
    private const int AnimeID = 77;

    private const string Xml = """
        <anime id="77" restricted="false">
          <type>Movie</type>
          <episodecount>1</episodecount>
          <startdate>1965-04-01</startdate>
          <titles><title xml:lang="x-jat" type="main">Old Movie</title></titles>
          <episodes>
            <episode id="1"><epno>1</epno><length>90</length><airdate>1970-01-01</airdate></episode>
            <episode id="2"><epno>S1</epno><length>5</length></episode>
            <episode id="3"><epno>S2</epno><length>5</length><airdate>1970-01-01</airdate></episode>
          </episodes>
        </anime>
        """;

    private static readonly HttpAnimeParser _parser = new(NullLogger<HttpAnimeParser>.Instance);

    private static AniDB_Episode Episode(int id, int? airDate, EpisodeType type = EpisodeType.Episode)
        => new() { AniDB_EpisodeID = id, EpisodeID = id, AnimeID = AnimeID, EpisodeType = type, AirDate = airDate };

    [Fact]
    public void OnlyTheUndatedEpisodesTheXmlGivesThePlaceholderAreFound()
    {
        AniDB_Episode[] undated = [Episode(1, null), Episode(2, null, EpisodeType.Special)];

        var found = DatabaseFixes.FindPlaceholderAirDates(AnimeID, undated, _ => Xml, _parser);

        Assert.Equal([undated[0]], found);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("<anime id=\"77\"><broken>")]
    public void AMissingOrBrokenXmlIsUnread(string? xml)
        => Assert.Null(DatabaseFixes.FindPlaceholderAirDates(AnimeID, [Episode(1, null)], _ => xml, _parser));
}

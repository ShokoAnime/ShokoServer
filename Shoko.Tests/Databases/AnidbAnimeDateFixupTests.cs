using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Databases;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Providers.AniDB.HTTP;
using Xunit;

namespace Shoko.Tests.Databases;

/// <summary>
/// The data fix reading the AniDB anime start and end dates back from the cached anime XML, and
/// queuing a refresh of each anime holding the 1970-01-01 placeholder without a readable one.
/// </summary>
public class AnidbAnimeDateFixupTests
{
    private static readonly HttpAnimeParser _parser = new(NullLogger<HttpAnimeParser>.Instance);

    private static readonly PartialDateOnly _placeholder = new(1970, 1, 1);

    private static string Xml(int animeID, string startDate, string endDate) => $"""
        <anime id="{animeID}" restricted="false">
          <type>Movie</type>
          <episodecount>1</episodecount>
          <startdate>{startDate}</startdate>
          <enddate>{endDate}</enddate>
          <titles><title xml:lang="x-jat" type="main">Old Movie</title></titles>
        </anime>
        """;

    [Fact]
    public void TheDatesAreReadBackFromTheCacheAndTheRestQueuedOrLeftAlone()
    {
        var cache = new Dictionary<int, string>
        {
            [1] = Xml(1, "1970-01-01", "1970-01-01"),
            [2] = Xml(2, "1970", "1970"),
            [3] = Xml(3, "1965-04-01", "1965-04-01"),
        };
        var placeholder = new AniDB_Anime { AnimeID = 1, AirDate = _placeholder, EndDate = _placeholder, BeginYear = 1970, EndYear = 1970 };
        var yearOnly = new AniDB_Anime { AnimeID = 2 };
        var unchanged = new AniDB_Anime { AnimeID = 3, AirDate = new(1965, 4, 1), EndDate = new(1965, 4, 1), BeginYear = 1965, EndYear = 1965 };
        var uncachedPlaceholder = new AniDB_Anime { AnimeID = 4, AirDate = _placeholder, BeginYear = 1970 };
        var uncachedUndated = new AniDB_Anime { AnimeID = 5 };
        var saved = new List<AniDB_Anime>();
        var queued = new List<int>();

        var result = DatabaseFixes.FixAnidbAnimeDates(
            [placeholder, yearOnly, unchanged, uncachedPlaceholder, uncachedUndated],
            animeID => cache.GetValueOrDefault(animeID),
            _parser,
            saved.Add,
            queued.Add
        );

        Assert.Equal([placeholder, yearOnly], saved);
        Assert.Equal((null, null, 0, 0), (placeholder.AirDate, placeholder.EndDate, placeholder.BeginYear, placeholder.EndYear));
        var year = new PartialDateOnly(1970);
        Assert.Equal((year, year, 1970, 1970), (yearOnly.AirDate, yearOnly.EndDate, yearOnly.BeginYear, yearOnly.EndYear));
        Assert.Equal([4], queued);
        Assert.Equal(new DatabaseFixes.AnidbAnimeDatesResult(2, 1, 1, 1), result);
    }
}

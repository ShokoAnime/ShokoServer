using System;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Tests.Infrastructure;
using Xunit;

using AniDBExtensions = Shoko.Server.Providers.AniDB.AniDBExtensions;

namespace Shoko.Tests.Models;

/// <summary>
/// An AniDB episode's air date: <c>null</c> when AniDB gave none, <c>0</c> for
/// AniDB's 1970-01-01 placeholder, both reading as no date unless the anime's
/// own date stands in for an undated episode before 1970.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class AnidbEpisodeAirDateTests
{
    #region Fixtures

    private const int AnimeID = 4242;

    private static int Seconds(int year, int month, int day)
        => AniDBExtensions.GetAniDBDateAsSeconds(new DateTime(year, month, day));

    private static AniDB_Episode Episode(int number, int? airDate, EpisodeType type = EpisodeType.Episode) => new()
    {
        AniDB_EpisodeID = number + (type is EpisodeType.Episode ? 0 : 100),
        EpisodeID = 1000 + number + (type is EpisodeType.Episode ? 0 : 100),
        AnimeID = AnimeID,
        EpisodeType = type,
        EpisodeNumber = number,
        LengthSeconds = 1440,
        AirDate = airDate,
    };

    private static RepoFactoryScope Scope(PartialDateOnly? animeDate, params AniDB_Episode[] episodes)
        => new RepoFactoryScope()
            .With<AniDB_AnimeRepository, int, AniDB_Anime>(
                anime => anime.AniDB_AnimeID,
                [new AniDB_Anime { AniDB_AnimeID = 1, AnimeID = AnimeID, AnimeType = AnimeType.Movie, AirDate = animeDate }]
            )
            .With<AniDB_EpisodeRepository, int, AniDB_Episode>(episode => episode.AniDB_EpisodeID, episodes);

    #endregion

    #region Readers

    [Fact]
    public void ThePlaceholderAndAMissingDateReadAsNoDate()
    {
        Assert.Null(AniDBExtensions.GetAniDBDateAsDate(0));
        Assert.Null(AniDBExtensions.GetAniDBDateAsDate(null));

        var placeholder = Episode(1, 0);
        var undated = Episode(2, null);
        using var scope = Scope(new PartialDateOnly(2020, 1, 5), placeholder, undated);

        Assert.True(placeholder.HasPlaceholderAirDate);
        Assert.False(undated.HasPlaceholderAirDate);
        foreach (var episode in new[] { placeholder, undated })
        {
            Assert.Null(episode.GetAirDateAsDate());
            Assert.Null(episode.GetAirDateAsDateOnly());
            Assert.Null(episode.GetAirDateAsPartialDateOnly());
            Assert.Null(((IEpisode)episode).AirDate);
        }
    }

    [Theory]
    [InlineData(0, 1, "1965-04-01", "1965-04-01")]
    [InlineData(-86400, 1, "1965-04-01", "1965-04-01")]
    [InlineData(0, 1, "1970-01-01", "1970-01-01")]
    [InlineData(0, 2, "1965-04-01", null)]
    [InlineData(0, 1, "1965-04", null)]
    [InlineData(0, 1, "1975-04-01", null)]
    [InlineData(null, 1, "1965-04-01", "1965-04-01")]
    [InlineData(null, 2, "1965-04-01", null)]
    [InlineData(null, 1, "1975-04-01", null)]
    public void AnUndatedOnlyEpisodeOfAPre1970AnimeReadsAsTheAnimesDate(int? airDate, int regularEpisodes, string animeDate, string? expected)
    {
        var episodes = Enumerable.Range(1, regularEpisodes).Select(number => Episode(number, airDate)).Append(Episode(1, null, EpisodeType.Special)).ToArray();
        using var scope = Scope(PartialDateOnly.Parse(animeDate), episodes);

        var date = expected is null ? (DateOnly?)null : DateOnly.Parse(expected);
        Assert.Equal(date, episodes[0].GetAirDateAsDateOnly());
        Assert.Equal(date, ((IEpisode)episodes[0]).AirDate);
    }

    [Fact]
    public void ARealDateIsKeptOverTheAnimesDate()
    {
        var episode = Episode(1, Seconds(1980, 6, 1));
        using var scope = Scope(new PartialDateOnly(1965, 4, 1), episode);

        Assert.Equal(new DateOnly(1980, 6, 1), episode.GetAirDateAsDateOnly());
    }

    #endregion

    #region Checks

    [Fact]
    public void ASeriesWithoutAnAnimeDateStartsAtItsFirstDatedEpisode()
    {
        using var scope = Scope(null, Episode(1, null), Episode(2, 0), Episode(3, Seconds(2020, 1, 12)), Episode(4, Seconds(2020, 1, 5)));

        Assert.Equal(new PartialDateOnly(2020, 1, 5), new AnimeSeries { AniDB_ID = AnimeID }.AirDate);
    }

    #endregion
}

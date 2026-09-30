using System;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Tests.Infrastructure;
using Xunit;

using AnidbReleaseStatus = Shoko.Server.Providers.AniDB.AnidbReleaseStatus;

namespace Shoko.Tests.Providers.AniDB;

/// <summary>
/// Covers how an AniDB anime's release status is inferred, by
/// <see cref="AnidbReleaseStatus"/> and through the cached
/// <see cref="AniDB_Anime.ReleaseStatus"/>.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class AnidbReleaseStatusTests
{
    #region Rules

    private static readonly DateOnly Today = new(2026, 9, 26);

    private static ReleaseStatus Infer(
        PartialDateOnly? airDate,
        PartialDateOnly? endDate = null,
        AnimeType type = AnimeType.TV,
        int normalEpisodes = 0,
        params DateOnly?[] episodeAirDates
    )
        => AnidbReleaseStatus.Infer(airDate, endDate, type, normalEpisodes, episodeAirDates, Today);

    [Fact]
    public void NoAirDate_IsUnknown()
        => Assert.Equal(ReleaseStatus.Unknown, Infer(null, new PartialDateOnly(2020, 1, 1)));

    [Fact]
    public void AirDateAfterToday_IsNotYetReleased()
        => Assert.Equal(ReleaseStatus.NotYetReleased, Infer(new PartialDateOnly(2026, 9, 27)));

    [Fact]
    public void AirDateToday_HasStarted()
        => Assert.Equal(ReleaseStatus.Releasing, Infer(new PartialDateOnly(2026, 9, 26)));

    [Fact]
    public void EndDateOnOrBeforeToday_IsFinished()
    {
        Assert.Equal(ReleaseStatus.Finished, Infer(new PartialDateOnly(2026, 7, 1), new PartialDateOnly(2026, 9, 26)));
        Assert.Equal(ReleaseStatus.Finished, Infer(new PartialDateOnly(2020, 1, 1), new PartialDateOnly(2020, 3, 20)));
    }

    [Fact]
    public void EndDateAfterToday_IsReleasing()
        => Assert.Equal(ReleaseStatus.Releasing, Infer(new PartialDateOnly(2026, 7, 1), new PartialDateOnly(2026, 9, 27), normalEpisodes: 1));

    [Theory]
    [InlineData(2026, null, ReleaseStatus.NotYetReleased)]
    [InlineData(2026, 9, ReleaseStatus.NotYetReleased)]
    [InlineData(2026, 8, ReleaseStatus.Releasing)]
    [InlineData(2025, null, ReleaseStatus.Releasing)]
    public void PartialAirDate_CountsAsItsLastDay(int year, int? month, ReleaseStatus expected)
        => Assert.Equal(expected, Infer(new PartialDateOnly(year, month)));

    [Theory]
    [InlineData(2026, null, ReleaseStatus.Releasing)]
    [InlineData(2026, 9, ReleaseStatus.Releasing)]
    [InlineData(2026, 8, ReleaseStatus.Finished)]
    [InlineData(2025, null, ReleaseStatus.Finished)]
    public void PartialEndDate_CountsAsItsLastDay(int year, int? month, ReleaseStatus expected)
        => Assert.Equal(expected, Infer(new PartialDateOnly(2025, 1, 1), new PartialDateOnly(year, month)));

    [Fact]
    public void PartialDate_OnTheLastDayOfItsPeriod_HasPassed()
    {
        Assert.True(AnidbReleaseStatus.HasPassed(new PartialDateOnly(2026, 9), new DateOnly(2026, 9, 30)));
        Assert.True(AnidbReleaseStatus.HasPassed(new PartialDateOnly(2026), new DateOnly(2026, 12, 31)));
        Assert.False(AnidbReleaseStatus.HasEnded(null, Today));
    }

    [Fact]
    public void MovieWithoutEndDate_IsFinishedOnceItAired()
    {
        Assert.Equal(ReleaseStatus.Finished, Infer(new PartialDateOnly(2026, 9, 1), type: AnimeType.Movie));
        Assert.Equal(ReleaseStatus.NotYetReleased, Infer(new PartialDateOnly(2026, 10, 1), type: AnimeType.Movie));
    }

    [Fact]
    public void SingleEpisodeWithoutEndDate_IsFinishedOnceItAired()
        => Assert.Equal(ReleaseStatus.Finished, Infer(new PartialDateOnly(2026, 9, 1), type: AnimeType.OVA, normalEpisodes: 1));

    [Fact]
    public void AllEpisodesAiredWithoutEndDate_IsFinished()
        => Assert.Equal(ReleaseStatus.Finished, Infer(
            new PartialDateOnly(2026, 7, 1), normalEpisodes: 3,
            episodeAirDates: [new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 8), new DateOnly(2026, 9, 26)]
        ));

    [Fact]
    public void SomeEpisodesUnairedOrMissing_IsReleasing()
    {
        Assert.Equal(ReleaseStatus.Releasing, Infer(
            new PartialDateOnly(2026, 7, 1), normalEpisodes: 3,
            episodeAirDates: [new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 8), new DateOnly(2026, 9, 27)]
        ));
        Assert.Equal(ReleaseStatus.Releasing, Infer(
            new PartialDateOnly(2026, 7, 1), normalEpisodes: 3,
            episodeAirDates: [new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 8)]
        ));
        Assert.Equal(ReleaseStatus.Releasing, Infer(
            new PartialDateOnly(2026, 7, 1), normalEpisodes: 2,
            episodeAirDates: [new DateOnly(2026, 7, 1), null]
        ));
    }

    [Fact]
    public void UnknownEpisodeCount_IsReleasing()
        => Assert.Equal(ReleaseStatus.Releasing, Infer(new PartialDateOnly(2020, 1, 1), episodeAirDates: [new DateOnly(2020, 1, 1)]));

    #endregion

    #region Model

    private const int AnimeID = 42;

    private static AniDB_Episode Episode(int id, DateTime? airDate) => new()
    {
        AniDB_EpisodeID = id,
        EpisodeID = id,
        AnimeID = AnimeID,
        EpisodeType = EpisodeType.Episode,
        EpisodeNumber = id,
        AirDate = airDate is { } date ? (int)(date - DateTime.UnixEpoch).TotalSeconds : 0,
    };

    private static RepoFactoryScope Scope(AniDB_Anime anime, params AniDB_Episode[] episodes)
        => new RepoFactoryScope()
            .With<AniDB_AnimeRepository, int, AniDB_Anime>(a => a.AniDB_AnimeID, [anime])
            .With<AnimeSeriesRepository, int, AnimeSeries>(s => s.AnimeSeriesID, [new() { AnimeSeriesID = 7, AniDB_ID = AnimeID }])
            .With<AniDB_EpisodeRepository, int, AniDB_Episode>(e => e.AniDB_EpisodeID, episodes);

    [Fact]
    public void Anime_ReadsItsEpisodes_AndTheShokoSeriesReportsIt()
    {
        var anime = new AniDB_Anime
        {
            AniDB_AnimeID = 1,
            AnimeID = AnimeID,
            AnimeType = AnimeType.TV,
            AirDate = new PartialDateOnly(2020, 1, 1),
            EpisodeCountNormal = 2,
        };
        using var scope = Scope(anime, Episode(1, new(2020, 1, 1)), Episode(2, new(2020, 1, 8)));

        Assert.Equal(ReleaseStatus.Finished, ((ISeries)anime).ReleaseStatus);
        Assert.Equal(ReleaseStatus.Finished, ((ISeries)RepoFactory.AnimeSeries.GetByID(7)!).ReleaseStatus);
    }

    [Fact]
    public void Anime_KeepsItsValueUntilItIsImportedAgain()
    {
        var anime = new AniDB_Anime
        {
            AniDB_AnimeID = 1,
            AnimeID = AnimeID,
            AnimeType = AnimeType.TV,
            AirDate = new PartialDateOnly(2020, 1, 1),
            EpisodeCountNormal = 2,
        };
        using var scope = Scope(anime, Episode(1, new(2020, 1, 1)));
        Assert.Equal(ReleaseStatus.Releasing, anime.ReleaseStatus);

        anime.EndDate = new PartialDateOnly(2020, 1, 8);
        Assert.Equal(ReleaseStatus.Releasing, anime.ReleaseStatus);

        anime.ResetReleaseStatus();
        Assert.Equal(ReleaseStatus.Finished, anime.ReleaseStatus);
    }

    #endregion
}

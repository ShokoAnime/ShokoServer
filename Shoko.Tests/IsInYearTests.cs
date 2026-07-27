using System;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Extensions;
using Shoko.Server.Models.AniDB;
using Xunit;

namespace Shoko.Tests;

/// <summary>
/// Covers <see cref="Models.IsInYear"/>, which backs <c>AnimeSeries.Years</c> (and the legacy
/// <c>Stat_AllYears</c> contract) -- the "Years" counterpart to the "Seasons" bugs covered in
/// <see cref="YearlySeasonsTests"/>. It shared the exact same root cause: a null <c>EndDate</c> was read as
/// "still airing" even for Movie/OVA/etc., where AniDB routinely just never populates it.
/// </summary>
public class IsInYearTests
{
    private static AniDB_Anime MakeAnime(PartialDateOnly? airDate, PartialDateOnly? endDate, AnimeType animeType = AnimeType.TV, int episodeCountNormal = 12)
        => new()
        {
            AirDate = airDate,
            EndDate = endDate,
            AnimeType = animeType,
            EpisodeCountNormal = episodeCountNormal,
        };

    [Fact]
    public void NullAirDate_IsNeverInAnyYear()
    {
        var anime = MakeAnime(null, null);

        Assert.False(anime.IsInYear(2024));
    }

    [Fact]
    public void FullPrecisionRun_IsInItsOwnStartYear()
    {
        var anime = MakeAnime(new PartialDateOnly(2024, 3, 29), new PartialDateOnly(2024, 3, 29));

        Assert.True(anime.IsInYear(2024));
        Assert.False(anime.IsInYear(2023));
        Assert.False(anime.IsInYear(2025));
    }

    [Fact]
    public void MultiYearRun_IsInEveryYearItSpans()
    {
        var anime = MakeAnime(new PartialDateOnly(2010, 1, 8), new PartialDateOnly(2013, 3, 26));

        Assert.True(anime.IsInYear(2010));
        Assert.True(anime.IsInYear(2011));
        Assert.True(anime.IsInYear(2012));
        Assert.True(anime.IsInYear(2013));
        Assert.False(anime.IsInYear(2014));
    }

    [Fact]
    public void OngoingTvSeries_WithNullEndDate_IsInEveryYearUpToNow()
    {
        var today = DateTime.Today;
        var anime = MakeAnime(new PartialDateOnly(today.Year - 3, 4, 6), null, AnimeType.TV);

        Assert.True(anime.IsInYear(today.Year - 3));
        Assert.True(anime.IsInYear(today.Year - 2));
        Assert.True(anime.IsInYear(today.Year - 1));
    }

    [Theory]
    [InlineData(AnimeType.Movie)]
    [InlineData(AnimeType.OVA)]
    [InlineData(AnimeType.Web)]
    [InlineData(AnimeType.Other)]
    [InlineData(AnimeType.MusicVideo)]
    public void RealWorldRegression_MovieOrOvaWithNoEndDate_IsNotInLaterYears(AnimeType animeType)
    {
        // Reproduces the production case: an OVA aired 2024-12-27 with no recorded EndDate used to be
        // considered "in year" for every year from 2024 all the way through to today, because a null
        // EndDate was read as "still airing". A single-day release only belongs to its own year.
        var anime = MakeAnime(new PartialDateOnly(2024, 12, 27), null, animeType, episodeCountNormal: 2);

        Assert.True(anime.IsInYear(2024));
        Assert.False(anime.IsInYear(2025));
        Assert.False(anime.IsInYear(2026));
    }

    [Theory]
    [InlineData(AnimeType.TV)]
    [InlineData(AnimeType.TVSpecial)]
    public void BroadcastTypesWithNoEndDate_StillCountAsOngoing(AnimeType animeType)
    {
        // Broadcast types keep the original "still airing" semantics: a null EndDate genuinely can mean
        // the show hasn't finished yet.
        var anime = MakeAnime(new PartialDateOnly(2024, 1, 8), null, animeType);

        Assert.True(anime.IsInYear(2024));
        Assert.True(anime.IsInYear(2025));
    }
}

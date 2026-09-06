using System;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Extensions;
using Shoko.Server.Models.AniDB;
using Xunit;

namespace Shoko.Tests;

/// <summary>
/// Covers <see cref="Models.GetYearlySeasons(PartialDateOnly?, PartialDateOnly?)"/> (and its overloads), which
/// backs every "season" filter (<c>InSeasonExpression</c>, <c>Seasons</c>/<c>Years</c> filterable properties).
/// Anime dates are frequently partial (year-only or year+month, per AniDB), and <see cref="PartialDateOnly.ToDateOnly"/>
/// resolves missing components to 1 (January 1st for a year-only date, the 1st of the month for a year+month date).
///
/// Each season's "did it start in this season" window used to run all the way to the raw start of the *next*
/// quarter (e.g. Spring's window was Mar 2 - Jul 1) instead of stopping at that next season's own buffered
/// start (Jun 1). That gave every season an extra, un-buffered month of overlap with the next one -- any show
/// starting anywhere in the back third of a season (e.g. all of June for Spring/Summer) was double-counted
/// into both. <see cref="RealWorldRegression_LateSeasonPremiere_IsClassifiedOnlyUnderItsOwnSeason"/> reproduces
/// the exact production case (a Summer premiere also showing up under Spring).
/// </summary>
public class YearlySeasonsTests
{
    [Fact]
    public void NullAirDate_ReturnsNoSeasons()
    {
        PartialDateOnly? airDate = null;
        var seasons = airDate.GetYearlySeasons(null);

        Assert.Empty(seasons);
    }

    [Theory]
    [InlineData(1, 15, 2, 15, YearlySeason.Winter)]
    [InlineData(4, 15, 5, 15, YearlySeason.Spring)]
    [InlineData(7, 15, 8, 15, YearlySeason.Summer)]
    [InlineData(10, 15, 11, 15, YearlySeason.Fall)]
    public void FullPrecisionRun_WellInsideOneSeason_ReturnsOnlyThatSeason(
        int startMonth, int startDay, int endMonth, int endDay, YearlySeason expected)
    {
        var airDate = new PartialDateOnly(2015, startMonth, startDay);
        var endDate = new PartialDateOnly(2015, endMonth, endDay);

        var seasons = airDate.GetYearlySeasons(endDate);

        Assert.Equal([(2015, expected)], seasons);
    }

    [Fact]
    public void RealWorldRegression_LateSeasonPremiere_IsClassifiedOnlyUnderItsOwnSeason()
    {
        // Reproduces a production case: a show that premiered June 26th is unambiguously a
        // Summer show, but used to also be returned for Spring because Spring's window ran all the way
        // to July 1st (a full extra month past Summer's own June 1st buffered start). The short end date
        // isolates "which season did the premiere start in" -- without it, a null EndDate reads as
        // "still airing" and every season whose buffered start has since passed (e.g. Fall after Sep 1)
        // would legitimately be included too, making this test fail on the calendar rather than the logic.
        var airDate = new PartialDateOnly(2026, 6, 26);
        var endDate = new PartialDateOnly(2026, 7, 10);

        var seasons = airDate.GetYearlySeasons(endDate);

        Assert.Equal([(2026, YearlySeason.Summer)], seasons);
    }

    [Theory]
    [InlineData(6, 3, YearlySeason.Summer)]
    [InlineData(6, 21, YearlySeason.Summer)]
    [InlineData(6, 30, YearlySeason.Summer)]
    [InlineData(3, 5, YearlySeason.Spring)]
    [InlineData(3, 20, YearlySeason.Spring)]
    [InlineData(9, 5, YearlySeason.Fall)]
    [InlineData(9, 20, YearlySeason.Fall)]
    [InlineData(12, 5, YearlySeason.Winter)]
    [InlineData(12, 20, YearlySeason.Winter)]
    public void RealWorldRegression_AnywhereInTheLaterPartOfASeason_IsNeverDoubleCounted(int month, int day, YearlySeason expected)
    {
        // Every one of these dates falls in what used to be the erroneous full-month overlap at the tail
        // of a season (the back part of Mar/Jun/Sep/Dec). A short, fully-finished run (not ongoing) isolates
        // "which season did this start in" -- each must resolve to exactly the one season it started in.
        var airDate = new PartialDateOnly(2015, month, day);
        var endDate = new PartialDateOnly(airDate.ToDateOnly().AddDays(14));
        var expectedYear = month == 12 ? 2016 : 2015;

        var seasons = airDate.GetYearlySeasons(endDate);

        Assert.Equal([(expectedYear, expected)], seasons);
    }

    [Fact]
    public void FullPrecisionRun_CrossingWinterYearBoundary_IsClassifiedUnderTheLaterYear()
    {
        // Aired late December through mid-January: Winter's buffer starts a week early (Dec 25), so this
        // is "Winter <next year>" only -- Dec 25 is comfortably past Fall's own end (Dec 2), so there's no
        // overlap with Fall of the start year.
        var airDate = new PartialDateOnly(2015, 12, 25);
        var endDate = new PartialDateOnly(2016, 1, 15);

        var seasons = airDate.GetYearlySeasons(endDate);

        Assert.Equal([(2016, YearlySeason.Winter)], seasons);
    }

    [Fact]
    public void FullPrecisionRun_EndingExactlyOnJanuaryFirst_ExcludesTheEndingYear()
    {
        // A run that stops right as the new year begins doesn't continue far enough into the new year's
        // Winter (the "continues well into the season" buffer requires ~46 days past the season's nominal
        // start) to count. This is the general heuristic, not specific to partial dates -- it's asserted
        // here as a baseline to compare against the year-only-precision case below, which hits this same
        // rule via the January 1st fallback rather than a real recorded date.
        var airDate = new PartialDateOnly(1998, 1, 8);
        var endDate = new PartialDateOnly(1999, 1, 1);

        var seasons = airDate.GetYearlySeasons(endDate);

        Assert.Equal([(1998, YearlySeason.Winter), (1998, YearlySeason.Spring), (1998, YearlySeason.Summer), (1998, YearlySeason.Fall)], seasons);
    }

    [Fact]
    public void YearOnlyPrecision_SameYearForAirAndEndDate_FallsBackToJanuaryFirstForBoth()
    {
        // Both dates resolve to January 1st, comfortably inside Winter's window (Dec 2 <year-1> - Mar 2
        // <year>) and nowhere near either boundary, so this is unambiguously Winter only.
        var airDate = new PartialDateOnly(1998);
        var endDate = new PartialDateOnly(1998);

        var seasons = airDate.GetYearlySeasons(endDate);

        Assert.Equal([(1998, YearlySeason.Winter)], seasons);
    }

    [Fact]
    public void YearOnlyPrecision_AirDateAndLaterYearEndDate_IncludesEveryStartYearSeasonButExcludesEndYear()
    {
        // AirDate=1998, EndDate=1999 (both year-only, i.e. 1998-01-01 and 1999-01-01). The show ran the
        // whole of 1998 (so all 4 of 1998's seasons are included), but because EndDate falls back to
        // January 1st 1999, it never continues far enough into any 1999 season for the buffer check to
        // count it -- 1999 is entirely excluded. This mirrors FullPrecisionRun_EndingExactlyOnJanuaryFirst
        // exactly, because both cases produce the identical effective date range; it's a real, if
        // surprising, consequence of collapsing an unknown end-of-year date down to January 1st.
        var airDate = new PartialDateOnly(1998);
        var endDate = new PartialDateOnly(1999);

        var seasons = airDate.GetYearlySeasons(endDate);

        Assert.Equal(
            [(1998, YearlySeason.Winter), (1998, YearlySeason.Spring), (1998, YearlySeason.Summer), (1998, YearlySeason.Fall)],
            seasons);
    }

    [Theory]
    [InlineData(1, YearlySeason.Winter)]
    [InlineData(2, YearlySeason.Winter)]
    [InlineData(4, YearlySeason.Spring)]
    [InlineData(5, YearlySeason.Spring)]
    [InlineData(7, YearlySeason.Summer)]
    [InlineData(8, YearlySeason.Summer)]
    [InlineData(10, YearlySeason.Fall)]
    [InlineData(11, YearlySeason.Fall)]
    public void MonthOnlyPrecision_FallsBackToTheFirstOfTheMonth_IsUnambiguousForMostMonths(int month, YearlySeason expectedSeason)
    {
        // Year+month precision (no day) falls back to day 1. For most months (including every season's
        // own first month -- Jan/Apr/Jul/Oct) day 1 is comfortably inside that season's window, nowhere
        // near a boundary, so exactly one season is returned.
        var airDate = new PartialDateOnly(2011, month);
        var endDate = new PartialDateOnly(2011, month);

        var seasons = airDate.GetYearlySeasons(endDate);

        Assert.Equal([(2011, expectedSeason)], seasons);
    }

    [Theory]
    [InlineData(6, YearlySeason.Spring, YearlySeason.Summer)]
    [InlineData(9, YearlySeason.Summer, YearlySeason.Fall)]
    public void MonthOnlyPrecision_ForJuneOrSeptember_LandsExactlyOnTheSharedBoundaryDay(int month, YearlySeason earlier, YearlySeason later)
    {
        // June and September are the two months whose "day 1" fallback happens to coincide exactly with
        // a season boundary (Spring/Summer's shared boundary is June 1st; Summer/Fall's is September 1st).
        // Both seasons are legitimately returned for that single day, since we don't know whether the real
        // (unrecorded) day was actually the 1st or later in the month.
        var airDate = new PartialDateOnly(2011, month);
        var endDate = new PartialDateOnly(2011, month);

        var seasons = airDate.GetYearlySeasons(endDate);

        Assert.Equal([(2011, earlier), (2011, later)], seasons);
    }

    [Fact]
    public void OngoingShow_WithNoEndDate_NeverReturnsASeasonThatHasNotStartedYet()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        PartialDateOnly? airDate = new PartialDateOnly(today.Year - 5, 4, 6);

        var seasons = airDate.GetYearlySeasons(null).ToList();

        Assert.NotEmpty(seasons);
        // Every returned season must be for a year at or before today's year; nothing from the future.
        Assert.All(seasons, s => Assert.True(s.Year <= today.Year));
    }

    [Fact]
    public void ShowAiringEntirelyInTheFuture_ReturnsNoSeasons()
    {
        // Deterministic regardless of when this test runs: year 9000 is always in the future.
        PartialDateOnly? airDate = new PartialDateOnly(9000, 1, 1);

        var seasons = airDate.GetYearlySeasons(null);

        Assert.Empty(seasons);
    }

    [Fact]
    public void MultiYearRun_ReturnsAllFourSeasonsForEveryFullyCoveredMiddleYear()
    {
        var airDate = new PartialDateOnly(2010, 1, 8);
        var endDate = new PartialDateOnly(2013, 3, 26);

        var seasons = airDate.GetYearlySeasons(endDate).ToList();

        // 2011 and 2012 are fully spanned, so all 4 seasons of each must be present.
        // (YearlySeason.Autumn is excluded here as it's just an alias for Fall with the same underlying value.)
        foreach (var year in new[] { 2011, 2012 })
        foreach (var season in new[] { YearlySeason.Winter, YearlySeason.Spring, YearlySeason.Summer, YearlySeason.Fall })
            Assert.Contains((year, season), seasons);
    }

    private static AniDB_Anime MakeAnime(PartialDateOnly? airDate, PartialDateOnly? endDate, AnimeType animeType = AnimeType.TV)
        => new() { AirDate = airDate, EndDate = endDate, AnimeType = animeType };

    [Theory]
    [InlineData(AnimeType.Movie)]
    [InlineData(AnimeType.OVA)]
    [InlineData(AnimeType.Web)]
    [InlineData(AnimeType.Other)]
    [InlineData(AnimeType.MusicVideo)]
    public void EffectiveEndDateForSeasons_NullEndDate_FallsBackToAirDateForNonBroadcastTypes(AnimeType animeType)
    {
        // AniDB routinely leaves EndDate unset for these types even long after they've fully released (see
        // RealWorldRegression_MovieOrOvaWithNoEndDate_IsNotTreatedAsStillAiring) -- a null EndDate here means
        // "single-day release", not "still airing".
        var airDate = new PartialDateOnly(2024, 3, 29);
        var anime = MakeAnime(airDate, null, animeType);

        Assert.Equal(airDate, anime.EffectiveEndDateForSeasons);
    }

    [Theory]
    [InlineData(AnimeType.TV)]
    [InlineData(AnimeType.TVSpecial)]
    [InlineData(AnimeType.Unknown)]
    public void EffectiveEndDateForSeasons_NullEndDate_StaysOpenEndedForBroadcastTypes(AnimeType animeType)
    {
        var anime = MakeAnime(new PartialDateOnly(2024, 3, 29), null, animeType);

        Assert.Null(anime.EffectiveEndDateForSeasons);
    }

    [Fact]
    public void EffectiveEndDateForSeasons_KnownEndDate_IsAlwaysUsedRegardlessOfType()
    {
        var endDate = new PartialDateOnly(2024, 6, 15);
        var anime = MakeAnime(new PartialDateOnly(2024, 3, 29), endDate, AnimeType.Movie);

        Assert.Equal(endDate, anime.EffectiveEndDateForSeasons);
    }

    [Fact]
    public void RealWorldRegression_MovieOrOvaWithNoEndDate_IsNotTreatedAsStillAiring()
    {
        // Reproduces a production case: an OVA that aired 2024-03-29 with no recorded EndDate used to show
        // up in every season from Spring 2024 all the way through to the current season, because a null
        // EndDate was read as "still airing". A movie/OVA release is a single-day event.
        var anime = MakeAnime(new PartialDateOnly(2024, 3, 29), null, AnimeType.OVA);

        var seasons = anime.AirDate.GetYearlySeasons(anime.EffectiveEndDateForSeasons);

        Assert.Equal([(2024, YearlySeason.Spring)], seasons);
    }

    [Fact]
    public void VeryOldYear_NearTheAssumedMinimumAnimeYear_DoesNotThrow()
    {
        // No real anime predates 1900; this guards the loop's backward-extension (for the previous-year
        // Fall boundary check, see MonthOnlyPrecision_ForJuneOrSeptember_LandsExactlyOnTheSharedBoundaryDay)
        // against underflowing into an invalid DateOnly year.
        var airDate = new PartialDateOnly(1900, 1, 1);
        var endDate = new PartialDateOnly(1900, 3, 1);

        var seasons = airDate.GetYearlySeasons(endDate);

        Assert.Equal([(1900, YearlySeason.Winter)], seasons);
    }

    [Fact]
    public void NonNullablePartialDateOnlyOverload_MatchesNullableOverload()
    {
        var airDate = new PartialDateOnly(2015, 4, 15);
        var endDate = new PartialDateOnly(2015, 5, 15);

        var viaValueTypes = airDate.GetYearlySeasons(endDate);
        var viaNullableTypes = ((PartialDateOnly?)airDate).GetYearlySeasons(endDate);

        Assert.Equal(viaNullableTypes, viaValueTypes);
    }

    [Fact]
    public void DateTimeOverload_MatchesPartialDateOnlyOverload()
    {
        DateTime? airDate = new DateTime(2015, 4, 15);
        DateTime? endDate = new DateTime(2015, 5, 15);

        var viaDateTime = airDate.GetYearlySeasons(endDate);
        var viaPartialDate = new PartialDateOnly(2015, 4, 15).GetYearlySeasons(new PartialDateOnly(2015, 5, 15));

        Assert.Equal(viaPartialDate, viaDateTime);
    }
}

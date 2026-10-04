using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Extensions;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Utilities;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests;

/// <summary>
/// Covers <see cref="SeasonCalendar"/>, the week-based season boundaries and
/// the rule placing an entry in the seasons, which every yearly season
/// consumer goes through.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class YearlySeasonsTests
{
    #region Fixtures

    private static readonly (int Year, YearlySeason Season) Spring = (2015, YearlySeason.Spring);

    private static readonly (int Year, YearlySeason Season) Summer = (2015, YearlySeason.Summer);

    // A cap far enough ahead to never cut a test's seasons short.
    private static readonly (int Year, YearlySeason Season) NoCap = (9000, YearlySeason.Fall);

    // Weekly air dates from a first day.
    private static IEnumerable<DateOnly> Weekly(DateOnly first, int count)
        => Enumerable.Range(0, count).Select(index => first.AddDays(7 * index));

    private static IReadOnlyList<(int Year, YearlySeason Season)> SeasonsOf(IEnumerable<DateOnly> regularAirDates)
        => SeasonCalendar.GetSeasons(SeasonCalendar.GetSpan(AnimeType.TV, regularAirDates, null, null), NoCap);

    private static IEnumerable<(int Year, YearlySeason Season)> AllSeasons(int fromYear, int toYear)
    {
        for (var season = (fromYear, YearlySeason.Winter); season.Item1 <= toYear; season = SeasonCalendar.GetNextYearlySeason(season))
            yield return season;
    }

    // Installs an anime and its normal episodes, numbered from one.
    private static RepoFactoryScope Install(AniDB_Anime anime, IEnumerable<DateOnly> dates)
    {
        var episodes = dates.Select((date, index) => new AniDB_Episode
        {
            AniDB_EpisodeID = index + 1,
            EpisodeID = index + 1,
            AnimeID = anime.AnimeID,
            EpisodeType = EpisodeType.Episode,
            EpisodeNumber = index + 1,
            AirDate = (int)(date.ToDateTime(TimeOnly.MinValue) - DateTime.UnixEpoch).TotalSeconds,
        });
        return new RepoFactoryScope()
            .With<AniDB_AnimeRepository, int, AniDB_Anime>(entry => entry.AniDB_AnimeID, [anime])
            .With<AniDB_EpisodeRepository, int, AniDB_Episode>(episode => episode.AniDB_EpisodeID, episodes);
    }

    #endregion

    #region Boundaries

    [Theory]
    [InlineData(AnimeType.TV)]
    [InlineData(AnimeType.Web)]
    [InlineData(AnimeType.Movie)]
    public void Seasons_TileTheCalendarInWholeWeeks(AnimeType type)
    {
        foreach (var season in AllSeasons(1990, 2040))
        {
            var start = SeasonCalendar.GetStart(season, type);
            var next = SeasonCalendar.GetStart(SeasonCalendar.GetNextYearlySeason(season), type);
            Assert.Equal(DayOfWeek.Monday, start.DayOfWeek);
            Assert.Equal(0, (next.DayNumber - start.DayNumber) % 7);
            Assert.Equal(season, SeasonCalendar.GetYearlySeason(start, type));
            Assert.Equal(season, SeasonCalendar.GetYearlySeason(next.AddDays(-1), type));
        }
    }

    [Theory]
    [InlineData(AnimeType.TV)]
    [InlineData(AnimeType.Web)]
    [InlineData(AnimeType.Movie)]
    public void Seasons_StartTheirTypesLeadInWeeksBeforeTheWeekOfTheirFirstDay(AnimeType type)
    {
        var leadIn = 7 * SeasonCalendar.GetLeadInWeeks(type);
        foreach (var season in AllSeasons(1990, 2040))
        {
            var firstDay = new DateOnly(season.Year, 1 + 3 * (int)season.Season, 1);
            Assert.InRange(firstDay.DayNumber - SeasonCalendar.GetStart(season, type).DayNumber, leadIn, leadIn + 6);
        }
    }

    [Theory]
    [InlineData(AnimeType.TV, 2026, 11, 29, 2026, YearlySeason.Fall)]
    [InlineData(AnimeType.TV, 2026, 11, 30, 2027, YearlySeason.Winter)]
    [InlineData(AnimeType.TV, 2026, 3, 1, 2026, YearlySeason.Winter)]
    [InlineData(AnimeType.TV, 2026, 3, 2, 2026, YearlySeason.Spring)]
    [InlineData(AnimeType.Web, 2026, 12, 20, 2026, YearlySeason.Fall)]
    [InlineData(AnimeType.Web, 2026, 12, 21, 2027, YearlySeason.Winter)]
    [InlineData(AnimeType.OVA, 2026, 12, 20, 2026, YearlySeason.Fall)]
    [InlineData(AnimeType.OVA, 2026, 12, 21, 2027, YearlySeason.Winter)]
    public void GetYearlySeason_SwitchesOnTheFirstMondayOfTheSeason(AnimeType type, int year, int month, int day, int expectedYear, YearlySeason expectedSeason)
        => Assert.Equal((expectedYear, expectedSeason), SeasonCalendar.GetYearlySeason(new DateOnly(year, month, day), type));

    #endregion

    #region Episode dates

    [Fact]
    public void TwelveEpisodes_LastThreeInTheNextQuarter_StayInTheFirst()
    {
        var dates = Weekly(new DateOnly(2026, 1, 29), 12).ToList();
        Assert.Equal(4, dates[^3].Month);

        Assert.Equal([(2026, YearlySeason.Winter)], SeasonsOf(dates));
    }

    [Fact]
    public void TwentyFourEpisodes_CrossingIntoTheNextQuarter_CountInBoth()
        => Assert.Equal([(2025, YearlySeason.Fall), (2026, YearlySeason.Winter)], SeasonsOf(Weekly(new DateOnly(2025, 10, 2), 24)));

    [Fact]
    public void StartInTheLeadInWeeks_EpisodesInOctober_OnlyFall()
        => Assert.Equal([(2026, YearlySeason.Fall)], SeasonsOf(Weekly(new DateOnly(2026, 9, 30), 5)));

    [Fact]
    public void QuartersWithoutEpisodes_AreLeftOut()
    {
        var span = SeasonCalendar.GetSpan(AnimeType.Movie, [new(2018, 10, 6), new(2019, 11, 8)], null, null);

        Assert.Equal([(2018, YearlySeason.Fall), (2019, YearlySeason.Fall)], SeasonCalendar.GetSeasons(span, NoCap));
    }

    [Fact]
    public void FewerThanFourEpisodes_CountWithAllOfThem()
        => Assert.Equal([Spring, Summer], SeasonsOf([new(2015, 5, 6), new(2015, 6, 3), new(2015, 7, 1)]));

    [Fact]
    public void EarlyEventScreening_DoesNotPullTheAnimeBackASeason()
    {
        const int animeID = 14987;
        var anime = new AniDB_Anime
        {
            AniDB_AnimeID = 1,
            AnimeID = animeID,
            AnimeType = AnimeType.TVSeries,
            AirDate = new PartialDateOnly(2015, 5, 10),
            Description = "[i]Note: The first two episodes were streamed by Funimation and Wakanim on May 10 for 24 hours. " +
                "The regular TV broadcast started on Jul 5, 2015.[/i]",
        };
        DateOnly[] dates = [new(2015, 5, 10), new(2015, 5, 10), .. Weekly(new DateOnly(2015, 7, 12), 11)];
        using var scope = Install(anime, dates);

        Assert.Equal(Spring, SeasonCalendar.GetYearlySeason(dates[0]));
        Assert.Equal([Summer], anime.YearlySeasons);
    }

    // The Idolmaster Shiny Colors 2nd Season (18523): screened in three parts
    // over the Summer, broadcast in the Fall.
    [Fact]
    public void ScreeningInParts_PlacesTheAnimeByItsBroadcast()
    {
        var anime = new AniDB_Anime
        {
            AniDB_AnimeID = 1,
            AnimeID = 18523,
            AnimeType = AnimeType.TVSeries,
            AirDate = new PartialDateOnly(2024, 7, 5),
            EndDate = new PartialDateOnly(2024, 9, 20),
            Description = "Note: It received an advance screening in theatres in three parts with episodes 1-4 having aired on 5 July 2024, " +
                "episodes 5-8 on 23 August 2024, and episodes 9-12 on 20 September 2024. The regular TV broadcast started on 5 October 2024.",
        };
        DateOnly[] parts = [new(2024, 7, 5), new(2024, 8, 23), new(2024, 9, 20)];
        using var scope = Install(anime, Enumerable.Range(0, 12).Select(index => parts[index / 4]));

        Assert.Equal([(2024, YearlySeason.Fall)], anime.YearlySeasons);
    }

    #endregion

    #region Start rules

    // The Fall 2026 boundary for web series: Monday 21 September.
    private static readonly DateOnly WebFall2026 = SeasonCalendar.GetStart((2026, YearlySeason.Fall), AnimeType.Web);

    private static (int Year, YearlySeason Season)? StartOf(AnimeType type, IEnumerable<DateOnly> regularAirDates)
        => SeasonCalendar.GetSpan(type, regularAirDates, null, null)?.StartSeason;

    [Fact]
    public void EarlyPremiere_TwoEpisodes_StartsInTheNextSeason()
        => Assert.Equal((2026, YearlySeason.Fall), StartOf(AnimeType.Web, [WebFall2026.AddDays(-10), WebFall2026.AddDays(-10), .. Weekly(WebFall2026, 10)]));

    [Fact]
    public void EarlyPremiere_OneEpisodeBeforeALongGap_StartsInTheNextSeason()
        => Assert.Equal((2026, YearlySeason.Fall), StartOf(AnimeType.Web, [WebFall2026.AddDays(-10), .. Weekly(WebFall2026.AddDays(50), 11)]));

    [Fact]
    public void EarlyPremiere_BatchFollowedByWeekly_StartsInTheNextSeason()
        => Assert.Equal((2026, YearlySeason.Fall), StartOf(AnimeType.Web, [.. Enumerable.Repeat(WebFall2026.AddDays(-7), 7), .. Weekly(WebFall2026, 6)]));

    // Yojouhan Time Machine Blues (16533): weekly from a week before Fall.
    [Fact]
    public void EarlyPremiere_OneEpisodeThenWeekly_StaysPut()
        => Assert.Equal((2022, YearlySeason.Summer), StartOf(AnimeType.Web, [.. Weekly(new DateOnly(2022, 9, 14), 5), new(2022, 10, 12)]));

    [Fact]
    public void BatchDrop_TwelveEpisodesAlone_TakesNoLeadIn()
        => Assert.Equal((2026, YearlySeason.Summer), StartOf(AnimeType.TV, Enumerable.Repeat(new DateOnly(2026, 9, 21), 12)));

    [Theory]
    [InlineData(4, 2026, YearlySeason.Winter)]
    [InlineData(5, 2025, YearlySeason.Fall)]
    public void BatchDrop_NeedsFiveEpisodes(int count, int year, YearlySeason season)
        => Assert.Equal((year, season), StartOf(AnimeType.TV, Enumerable.Repeat(new DateOnly(2025, 12, 28), count)));

    #endregion

    #region Dates only

    [Theory]
    [InlineData("2015-05-29", "2015-06-11", false)]
    [InlineData("2015-05-02", "2015-07-21", false)]
    [InlineData("2015-05-02", "2015-07-22", true)]
    public void DatesOnly_EndThreeWeeksEarly_NeverBeforeTheStart(string startDate, string endDate, bool reachesSummer)
    {
        var span = SeasonCalendar.GetSpan(
            AnimeType.TV,
            [],
            PartialDateOnly.Parse(startDate),
            PartialDateOnly.Parse(endDate)
        );

        Assert.True(span!.Last >= span.First);
        Assert.Equal(reachesSummer ? [Spring, Summer] : [Spring], SeasonCalendar.GetSeasons(span, NoCap));
    }

    [Fact]
    public void DatesOnly_PartialStart_FallsBackToTheFirstEpisode()
    {
        // A season starting late in a month, so the month's first day is still in the season before.
        (int Year, YearlySeason Season) winter = (2016, YearlySeason.Winter);
        var firstEpisode = SeasonCalendar.GetStart(winter);
        var monthOnly = new PartialDateOnly(firstEpisode.Year, firstEpisode.Month);
        Assert.NotEqual(winter, SeasonCalendar.GetYearlySeason(monthOnly.ToDateOnly()));

        var span = SeasonCalendar.GetSpan(AnimeType.TV, [], monthOnly, null, [firstEpisode.AddDays(14), firstEpisode]);

        Assert.Equal(winter, span!.StartSeason);
    }

    [Fact]
    public void DatesOnly_OpenEnd_RunsToTheCurrentSeason()
    {
        var today = new DateOnly(2020, 5, 1);
        var span = SeasonCalendar.GetSpan(AnimeType.TV, [], new PartialDateOnly(2019, 5, 1), null, today: today);

        Assert.Equal(today, span!.Last);
        Assert.Equal(SeasonCalendar.GetYearlySeason(today), SeasonCalendar.GetSeasons(span, SeasonCalendar.GetYearlySeason(today))[^1]);
    }

    [Fact]
    public void Seasons_AfterTheCap_AreLeftOut()
    {
        var span = SeasonCalendar.GetSpan(AnimeType.TV, Weekly(SeasonCalendar.GetStart(Summer), 12), null, null);

        Assert.Empty(SeasonCalendar.GetSeasons(span, Spring));
    }

    [Fact]
    public void NoDates_HaveNoSpan()
        => Assert.Null(SeasonCalendar.GetSpan(AnimeType.TV, [], null, new PartialDateOnly(2015, 5, 1)));

    [Fact]
    public void YearOnlyStart_WithoutEpisodes_HasNoSpan()
        => Assert.Null(SeasonCalendar.GetSpan(AnimeType.TV, [], new PartialDateOnly(2026), new PartialDateOnly(2026)));

    [Fact]
    public void StrayEpisodeLongBeforeTheStart_IsLeftOut()
    {
        var start = SeasonCalendar.GetStart(Summer).AddDays(10);
        var span = SeasonCalendar.GetSpan(AnimeType.Web, [start.AddDays(-270), .. Weekly(start, 12)], new PartialDateOnly(start.Year, start.Month, start.Day), null);

        Assert.Equal(start, span!.First);
    }

    #endregion

    #region Effective end date

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
        // AniDB routinely leaves EndDate unset for these types even long after they've fully released, so a
        // null EndDate here means "single-day release", not "still airing".
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

    #endregion
}

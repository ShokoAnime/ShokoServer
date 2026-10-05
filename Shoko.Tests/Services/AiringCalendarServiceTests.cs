using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Services;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers the grouping and sorting of <see cref="AiringCalendarService"/>: a
/// season's anime by section, and a range's airings by local day and episode.
/// </summary>
public class AiringCalendarServiceTests
{
    #region Fixtures

    private static readonly (int Year, YearlySeason Season) _fall2026 = (2026, YearlySeason.Fall);

    private static SeasonAnimeEntry Anime(
        int id,
        AnimeType type = AnimeType.TV,
        string? title = null,
        PartialDateOnly? airDate = null,
        (int Year, YearlySeason Season)? start = null,
        int? minutes = null,
        IEpisodeAiring? next = null
    )
        => new()
        {
            Anime = Mock.Of<IAnidbAnime>(anime => anime.AnidbID == id && anime.Type == type && anime.AirDate == airDate),
            Series = null,
            Title = title ?? $"Anime {id}",
            StartSeason = start,
            IsStartSeasonOverridden = false,
            EpisodeDuration = minutes is { } length ? TimeSpan.FromMinutes(length) : null,
            Status = SeasonAnimeAiringStatus.Unknown,
            NextAiring = next,
            OtherAirings = [],
        };

    // A timed airing of an AniDB episode, or a date-only entry when it has no time.
    private static IEpisodeAiring Airing(string key, int? episodeID, DateTime? airedAt, DateOnly? airDate = null, bool preferred = false)
    {
        var airing = new Mock<IEpisodeAiring>();
        airing.SetupGet(entry => entry.ID).Returns(Guid.NewGuid());
        airing.SetupGet(entry => entry.Key).Returns(key);
        airing.SetupGet(entry => entry.IsDateOnly).Returns(airedAt is null);
        airing.SetupGet(entry => entry.AirDate).Returns(airDate);
        airing.SetupGet(entry => entry.AiredAt).Returns(airedAt);
        airing.SetupGet(entry => entry.IsPreferred).Returns(preferred);
        airing.SetupGet(entry => entry.AnidbEpisode).Returns(
            episodeID is { } id
                ? Mock.Of<IAnidbEpisode>(episode => episode.ID == new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Episode, id.ToString()))
                : null
        );
        return airing.Object;
    }

    private static DateTime Utc(int day, int hour, int minute = 0)
        => new(2026, 10, day, hour, minute, 0, DateTimeKind.Utc);

    // The sections by ID, each with its anime's IDs in order.
    private static Dictionary<string, int[]> Group(IReadOnlyList<SeasonAnimeEntry> anime, IReadOnlyList<SeasonSectionDefinition>? sections = null)
        => AiringCalendarService.GroupIntoSections(anime, _fall2026, sections ?? SeasonSectionDefinition.DefaultLayout)
            .ToDictionary(section => section.Definition.ID, section => section.Anime.Select(entry => entry.Anime.AnidbID).ToArray());

    // A week from the 5th's local midnight, by day, each episode as its lead's key and its others' after a '>'.
    private static Dictionary<DateOnly, string[]> Days(
        IEnumerable<IEpisodeAiring> airings,
        TimeZoneInfo? zone = null,
        bool everyChannel = true
    )
    {
        zone ??= TimeZoneInfo.Utc;
        var start = new DateTimeOffset(new DateTime(2026, 10, 5), zone.BaseUtcOffset);
        return AiringCalendarService.GroupIntoDays(airings, start, start.AddDays(7), zone, everyChannel)
            .ToDictionary(
                day => day.Date,
                day => day.Episodes
                    .Select(episode => episode.Others.Count > 0
                        ? $"{episode.Lead.Airing.Key}>{string.Join(",", episode.Others.Select(other => other.Airing.Key))}"
                        : episode.Lead.Airing.Key)
                    .ToArray()
            );
    }

    private static DateOnly Day(int day)
        => new(2026, 10, day);

    #endregion

    #region Sections

    [Fact]
    public void Sections_SortByScheduledAiringThenAnidbDateThenPremiereAndTitle()
    {
        var anime = new[]
        {
            Anime(1, title: "Unknown"),
            Anime(2, title: "Later", airDate: new(2026, 10, 9)),
            Anime(3, title: "Second", airDate: new(2026, 10, 2)),
            Anime(4, title: "First", airDate: new(2026, 10, 2)),
            Anime(5, title: "Dated", airDate: new(2026, 10, 1), next: Airing("dated", 5, null, Day(6))),
            Anime(6, title: "Soon", airDate: new(2026, 10, 3), next: Airing("soon", 6, Utc(7, 15))),
            Anime(7, title: "Sooner", airDate: new(2026, 10, 4), next: Airing("sooner", 7, Utc(6, 15))),
        };

        Assert.Equal([7, 6, 5, 4, 3, 2, 1], Group(anime)["new"]);
    }

    [Fact]
    public void DefaultLayout_PutsEachAnimeInTheFirstSectionThatTakesIt_AndDropsEmptyOnes()
    {
        var anime = new[]
        {
            Anime(1, start: _fall2026, minutes: 24),
            // No known start or length: new and full length.
            Anime(2, AnimeType.Web),
            Anime(3, start: _fall2026, minutes: 5),
            // Continuing takes any length.
            Anime(4, start: (2026, YearlySeason.Summer), minutes: 5),
            Anime(5, AnimeType.OVA, start: _fall2026),
            Anime(6, AnimeType.TVSpecial),
            // Movies and the rest take older ones too: an episode or airing this season put them here.
            Anime(7, AnimeType.OVA, start: (2025, YearlySeason.Fall)),
            Anime(8, AnimeType.Movie, start: (2026, YearlySeason.Summer)),
        };

        var sections = Group(anime);

        Assert.Equal(["new", "new-half", "continuing", "movies", "other"], sections.Keys);
        Assert.Equal([1, 2], sections["new"]);
        Assert.Equal([3], sections["new-half"]);
        Assert.Equal([4], sections["continuing"]);
        Assert.Equal([8], sections["movies"]);
        Assert.Equal([5, 6, 7], sections["other"]);
    }

    [Fact]
    public void Sections_FirstOneWins_AndUnsetOptionsTakeBoth()
    {
        var layout = new SeasonSectionDefinition[]
        {
            new() { ID = "all-tv", Title = "TV", Types = new HashSet<AnimeType> { AnimeType.TV } },
            new() { ID = "rest", Title = "Rest" },
            // Never reached: the rest group took everything left.
            new() { ID = "movies", Title = "Movies", Types = new HashSet<AnimeType> { AnimeType.Movie } },
        };
        var anime = new[]
        {
            Anime(1, start: _fall2026, minutes: 24),
            Anime(2, start: (2026, YearlySeason.Spring), minutes: 5),
            Anime(3, AnimeType.Movie),
        };

        var sections = Group(anime, layout);

        Assert.Equal(["all-tv", "rest"], sections.Keys);
        Assert.Equal([1, 2], sections["all-tv"]);
        Assert.Equal([3], sections["rest"]);
    }

    [Fact]
    public void Sections_WithoutARestGroup_LeaveTheOthersOut()
    {
        var layout = new SeasonSectionDefinition[] { new() { ID = "movies", Title = "Movies", Types = new HashSet<AnimeType> { AnimeType.Movie } } };

        var sections = Group([Anime(1), Anime(2, AnimeType.Movie), Anime(3, AnimeType.OVA)], layout);

        Assert.Equal([2], Assert.Single(sections).Value);
    }

    [Theory]
    [InlineData(15, true)]
    [InlineData(16, false)]
    [InlineData(null, false)]
    public void HalfLength_IsUnderSixteenMinutes(int? minutes, bool halfLength)
    {
        var layout = new SeasonSectionDefinition[] { new() { ID = "half", Title = "Half", HalfLength = true } };

        Assert.Equal(halfLength, Group([Anime(1, minutes: minutes)], layout).ContainsKey("half"));
    }

    [Theory]
    [InlineData(2026, YearlySeason.Summer, true)]
    [InlineData(2025, YearlySeason.Fall, true)]
    [InlineData(2026, YearlySeason.Fall, false)]
    [InlineData(2027, YearlySeason.Winter, false)]
    [InlineData(null, null, false)]
    public void Continuing_MeansStartedBeforeTheViewedSeason(int? year, YearlySeason? season, bool continuing)
    {
        var layout = new SeasonSectionDefinition[] { new() { ID = "continuing", Title = "Continuing", Continuing = true } };
        var start = year is { } startYear && season is { } startSeason ? (startYear, startSeason) : ((int, YearlySeason)?)null;

        Assert.Equal(continuing, Group([Anime(1, start: start)], layout).ContainsKey("continuing"));
    }

    #endregion

    #region Days

    [Fact]
    public void Days_GroupAnEpisodeUnderItsEarliestAiring()
    {
        var airings = new[]
        {
            Airing("late", 1, Utc(5, 23, 45)),
            Airing("early", 1, Utc(5, 18, 30)),
            Airing("other", 2, Utc(5, 20)),
        };

        Assert.Equal(["early>late", "other"], Days(airings)[Day(5)]);
    }

    [Fact]
    public void Days_LeadWithThePreferredAiring_KeepingTheOthersInTimeOrder()
    {
        var airings = new[]
        {
            Airing("early", 1, Utc(5, 18, 30)),
            Airing("middle", 1, Utc(5, 20)),
            Airing("preferred", 1, Utc(5, 23, 45), preferred: true),
            Airing("other", 2, Utc(5, 21)),
        };

        Assert.Equal(["other", "preferred>early,middle"], Days(airings)[Day(5)]);
        Assert.Equal(["other", "preferred"], Days(airings, everyChannel: false)[Day(5)]);
    }

    [Fact]
    public void Days_LeadWithATimedAiringOverTheDateOnlyOne()
    {
        var airings = new[] { Airing("dated", 1, null, Day(5)), Airing("timed", 1, Utc(5, 18, 30)) };

        Assert.Equal(["timed>dated"], Days(airings)[Day(5)]);
    }

    [Fact]
    public void Days_KeepDifferentDaysAndUnknownEpisodesApart()
    {
        var airings = new[]
        {
            Airing("first", 1, Utc(5, 18, 30)),
            Airing("next", 1, Utc(6, 18, 30)),
            Airing("unknown", null, Utc(5, 19)),
            Airing("unknown-too", null, Utc(5, 20)),
        };

        var days = Days(airings);

        Assert.Equal(["first", "unknown", "unknown-too"], days[Day(5)]);
        Assert.Equal(["next"], days[Day(6)]);
    }

    [Fact]
    public void Days_AreTheTimeZonesOwn()
    {
        var tokyo = TimeZoneInfo.CreateCustomTimeZone("Test/Plus9", TimeSpan.FromHours(9), "UTC+9", "UTC+9");
        // 16:00 UTC is 01:00 the next day at UTC+9; a date-only entry stays on its date.
        var airings = new[] { Airing("late", 1, Utc(5, 16)), Airing("dated", 2, null, Day(5)) };

        Assert.Equal(["dated", "late"], Days(airings)[Day(5)]);
        var local = Days(airings, tokyo);
        Assert.Equal(["dated"], local[Day(5)]);
        Assert.Equal(["late"], local[Day(6)]);
    }

    #endregion
}

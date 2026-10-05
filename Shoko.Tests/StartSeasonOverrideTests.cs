using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Moq;
using Shoko.Abstractions.Filtering.Expressions.Info;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.API.v3.Models.AniDB;
using Shoko.Server.Filters;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Services;
using Shoko.Server.Utilities;
using Shoko.Tests.Infrastructure;
using Xunit;

using AbstractOverride = Shoko.Abstractions.Metadata.Anidb.Models.AnidbStartSeasonOverride;

namespace Shoko.Tests;

/// <summary>
/// Covers the start seasons users set by hand for AniDB anime: the rule
/// <see cref="SeasonCalendar.WithStartSeason"/> applies, the anime's cached
/// seasons and the filters reading them, the store behind the AniDB service,
/// and the CSV import and export.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class StartSeasonOverrideTests
{
    #region Fixtures

    private static readonly (int Year, YearlySeason Season) Winter2015 = (2015, YearlySeason.Winter);

    private static readonly (int Year, YearlySeason Season) Spring2015 = (2015, YearlySeason.Spring);

    private static readonly (int Year, YearlySeason Season) Summer2015 = (2015, YearlySeason.Summer);

    private static readonly (int Year, YearlySeason Season) Fall2015 = (2015, YearlySeason.Fall);

    // A computed span in Spring, Summer and Fall 2015.
    private static SeasonCalendar.SeasonSpan ThreeSeasons()
        => new(new(2015, 4, 6), new(2015, 10, 5), AnimeType.TV, Spring2015, [Spring2015, Summer2015, Fall2015]);

    // Weekly air dates from a first day.
    private static IEnumerable<DateOnly> Weekly(DateOnly first, int count)
        => Enumerable.Range(0, count).Select(index => first.AddDays(7 * index));

    private static AniDB_Anime Anime(int id, PartialDateOnly? airDate = null)
        => new() { AniDB_AnimeID = id, AnimeID = id, AnimeType = AnimeType.TVSeries, AirDate = airDate };

    private static AniDB_Episode Episode(int animeID, int number, DateOnly date)
        => new()
        {
            AniDB_EpisodeID = animeID * 1_000 + number,
            EpisodeID = animeID * 1_000 + number,
            AnimeID = animeID,
            EpisodeType = EpisodeType.Episode,
            EpisodeNumber = number,
            AirDate = (int)(date.ToDateTime(TimeOnly.MinValue) - DateTime.UnixEpoch).TotalSeconds,
        };

    /// <summary>
    /// Installs anime 1, airing weekly from 6 April 2015 in Spring and Summer
    /// 2015, anime 2 without any dates, and a writable override repository.
    /// </summary>
    private sealed class World : IDisposable
    {
        private readonly RepoFactoryScope _scope = new();

        private int _nextID = 1;

        public AniDB_Anime Airing { get; } = Anime(1, new(2015, 4, 6));

        public AniDB_Anime Undated { get; } = Anime(2);

        public Mock<AniDB_Anime_StartSeasonOverrideRepository> Overrides { get; }

        public World()
        {
            var episodes = Weekly(new(2015, 4, 6), 20).Select((date, index) => Episode(1, index + 1, date));
            _scope
                .With<AniDB_AnimeRepository, int, AniDB_Anime>(anime => anime.AniDB_AnimeID, [Airing, Undated])
                .With<AniDB_EpisodeRepository, int, AniDB_Episode>(episode => episode.AniDB_EpisodeID, episodes);
            Overrides = CachedRepo.BuildWritable<AniDB_Anime_StartSeasonOverrideRepository, int, AniDB_Anime_StartSeasonOverride>(
                row => row.AniDB_Anime_StartSeasonOverrideID
            );
            var repository = Overrides.Object;
            Overrides
                .Setup(entry => entry.Save(It.IsAny<AniDB_Anime_StartSeasonOverride>()))
                .Callback<AniDB_Anime_StartSeasonOverride>(row =>
                {
                    if (row.AniDB_Anime_StartSeasonOverrideID is 0)
                        row.AniDB_Anime_StartSeasonOverrideID = _nextID++;

                    repository.Cache.Update(row);
                });
            _scope.Set(repository);
        }

        public AnidbStartSeasonOverrides Store()
            => new(Overrides.Object);

        public void Dispose()
            => _scope.Dispose();
    }

    /// <summary>
    /// An AniDB service keeping its overrides in a dictionary.
    /// </summary>
    private static Mock<IAnidbService> InMemoryService(Dictionary<int, AbstractOverride> overrides)
    {
        var service = new Mock<IAnidbService>();
        service
            .Setup(entry => entry.GetStartSeasonOverride(It.IsAny<int>()))
            .Returns((int id) => overrides.GetValueOrDefault(id));
        service
            .Setup(entry => entry.GetStartSeasonOverrides())
            .Returns(() => [.. overrides.Values.OrderBy(entry => entry.AnidbAnimeID)]);
        service
            .Setup(entry => entry.SetStartSeasonOverride(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<YearlySeason>()))
            .Returns((int id, int year, YearlySeason season) => overrides[id] = new(id, year, season, DateTime.UnixEpoch, DateTime.UnixEpoch, null));
        return service;
    }

    #endregion

    #region The Rule

    [Fact]
    public void LaterStart_DropsTheComputedSeasonsBeforeIt()
    {
        var span = SeasonCalendar.WithStartSeason(ThreeSeasons(), Summer2015, AnimeType.TV);

        Assert.Equal(Summer2015, span.StartSeason);
        Assert.Equal([Summer2015, Fall2015], span.Seasons);
        Assert.True(span.IsStartOverridden);
    }

    [Fact]
    public void LaterStart_PastTheComputedSeasons_IsTheOnlySeason()
    {
        var span = SeasonCalendar.WithStartSeason(ThreeSeasons(), (2016, YearlySeason.Spring), AnimeType.TV);

        Assert.Equal([(2016, YearlySeason.Spring)], span.Seasons);
    }

    [Fact]
    public void EarlierStart_KeepsEveryComputedSeason_WithNoneFilledInBetween()
    {
        Assert.Equal(
            [Winter2015, Spring2015, Summer2015, Fall2015],
            SeasonCalendar.WithStartSeason(ThreeSeasons(), Winter2015, AnimeType.TV).Seasons
        );
        Assert.Equal(
            [(2014, YearlySeason.Summer), Spring2015, Summer2015, Fall2015],
            SeasonCalendar.WithStartSeason(ThreeSeasons(), (2014, YearlySeason.Summer), AnimeType.TV).Seasons
        );
    }

    [Fact]
    public void SameStart_KeepsTheSeasons_ButIsFlagged()
    {
        var span = SeasonCalendar.WithStartSeason(ThreeSeasons(), Spring2015, AnimeType.TV);

        Assert.Equal(ThreeSeasons().Seasons, span.Seasons);
        Assert.True(span.IsStartOverridden);
        Assert.False(ThreeSeasons().IsStartOverridden);
    }

    [Fact]
    public void WithoutAComputedSpan_TheOverriddenSeasonIsTheOnlyOne()
    {
        var span = SeasonCalendar.WithStartSeason(null, Summer2015, AnimeType.Movie);

        Assert.Equal([Summer2015], span.Seasons);
        Assert.Equal(new DateOnly(2015, 7, 1), span.First);
        Assert.Equal(AnimeType.Movie, span.Type);
    }

    #endregion

    #region The Anime

    [Fact]
    public void Anime_TheOverrideWinsForTheStart_AndTheComputedSpanStays()
    {
        using var world = new World();
        world.Store().Set(1, 2015, YearlySeason.Summer, null, DateTime.UtcNow);

        Assert.Equal(Summer2015, world.Airing.SeasonSpan!.StartSeason);
        Assert.True(world.Airing.SeasonSpan.IsStartOverridden);
        Assert.Equal([Summer2015], world.Airing.YearlySeasons);
        Assert.Equal(Spring2015, world.Airing.ComputedSeasonSpan!.StartSeason);
        Assert.False(world.Airing.ComputedSeasonSpan.IsStartOverridden);
    }

    [Fact]
    public void Anime_TheCachedSeasonsFollowTheOverride()
    {
        using var world = new World();
        var store = world.Store();

        Assert.Equal([Spring2015, Summer2015], world.Airing.YearlySeasons);

        store.Set(1, 2015, YearlySeason.Winter, null, DateTime.UtcNow);
        Assert.Equal([Winter2015, Spring2015, Summer2015], world.Airing.YearlySeasons);

        store.Set(1, 2015, YearlySeason.Summer, null, DateTime.UtcNow);
        Assert.Equal([Summer2015], world.Airing.YearlySeasons);

        store.Remove(1);
        Assert.Equal([Spring2015, Summer2015], world.Airing.YearlySeasons);
        Assert.False(world.Airing.SeasonSpan!.IsStartOverridden);
    }

    [Fact]
    public void Anime_WithoutDates_IsPlacedInTheOverriddenSeason()
    {
        using var world = new World();
        Assert.Empty(world.Undated.YearlySeasons);

        world.Store().Set(2, 2015, YearlySeason.Fall, null, DateTime.UtcNow);

        Assert.Equal([Fall2015], world.Undated.YearlySeasons);
        Assert.Null(world.Undated.ComputedSeasonSpan);
    }

    [Fact]
    public void Filters_SeriesAndGroupsSeeTheOverride()
    {
        using var world = new World();
        world.Store().Set(1, 2015, YearlySeason.Summer, null, DateTime.UtcNow);
        var series = new AnimeSeries { AnimeSeriesID = 10, AniDB_ID = 1 };
        var filterable = new FilterableAnimeSeries(series, DateTime.Now);

        Assert.True(new InSeasonExpression(2015, YearlySeason.Summer).Evaluate(filterable, null, null));
        Assert.False(new InSeasonExpression(2015, YearlySeason.Spring).Evaluate(filterable, null, null));
        Assert.Equal(Summer2015, Assert.Single(AnimeGroup.YearlySeasonsOf([series])));
    }

    #endregion

    #region The Store

    [Fact]
    public void Store_Set_AddsThenUpdates_KeepingWhenItWasCreated()
    {
        using var world = new World();
        var store = world.Store();
        var created = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var changed = created.AddDays(1);

        var (none, added) = store.Set(5, 2020, YearlySeason.Spring, 7, created);
        var (previous, updated) = store.Set(5, 2020, YearlySeason.Summer, 8, changed);

        Assert.Null(none);
        Assert.Equal(new AbstractOverride(5, 2020, YearlySeason.Spring, created, created, 7), added);
        Assert.Equal(added, previous);
        Assert.Equal(new AbstractOverride(5, 2020, YearlySeason.Summer, created, changed, 8), updated);
        Assert.Equal([updated], store.GetAll());
    }

    [Fact]
    public void Store_Set_TheSameSeason_ChangesNothing()
    {
        using var world = new World();
        var store = world.Store();
        store.Set(5, 2020, YearlySeason.Spring, 7, DateTime.UnixEpoch);

        var (previous, current) = store.Set(5, 2020, YearlySeason.Spring, 8, DateTime.UtcNow);

        Assert.Equal(previous, current);
        Assert.Equal(7, current.UserID);
        world.Overrides.Verify(entry => entry.Save(It.IsAny<AniDB_Anime_StartSeasonOverride>()), Times.Once);
    }

    [Fact]
    public void Store_Remove_ReturnsWhatWasRemoved()
    {
        using var world = new World();
        var store = world.Store();
        store.Set(5, 2020, YearlySeason.Spring, null, DateTime.UnixEpoch);

        Assert.Equal(YearlySeason.Spring, store.Remove(5)?.Season);
        Assert.Null(store.Remove(5));
        Assert.Null(store.Get(5));
    }

    [Theory]
    [InlineData(0, 2020, YearlySeason.Spring)]
    [InlineData(5, 1899, YearlySeason.Spring)]
    [InlineData(5, 10000, YearlySeason.Spring)]
    [InlineData(5, 2020, (YearlySeason)4)]
    public void Store_Set_RefusesValuesOutOfRange(int animeID, int year, YearlySeason season)
    {
        using var world = new World();

        Assert.Throws<ArgumentOutOfRangeException>(() => world.Store().Set(animeID, year, season, null, DateTime.UtcNow));
        world.Overrides.Verify(entry => entry.Save(It.IsAny<AniDB_Anime_StartSeasonOverride>()), Times.Never);
    }

    [Fact]
    public void ApiView_ShowsTheOverrideAndTheComputedStart()
    {
        var computed = ThreeSeasons();
        var overridden = AnidbStartSeason.From(computed, new(1, 2015, YearlySeason.Summer, DateTime.UnixEpoch, DateTime.UnixEpoch, null));
        var plain = AnidbStartSeason.From(computed, null);

        Assert.Equal((2015, YearlySeason.Summer, true), (overridden.Year, overridden.Season, overridden.IsOverridden));
        Assert.Equal((2015, YearlySeason.Spring), (overridden.Computed!.Year, overridden.Computed.Season));
        Assert.Equal((2015, YearlySeason.Spring, false), (plain.Year, plain.Season, plain.IsOverridden));
    }

    #endregion

    #region CSV

    [Fact]
    public void Csv_Export_WritesTheHeaderThenEachOverrideByAnimeID()
    {
        var text = AnidbStartSeasonOverrideCsv.Export(
            [
                new(20, 2001, YearlySeason.Fall, DateTime.UnixEpoch, DateTime.UnixEpoch, null),
                new(3, 1999, YearlySeason.Winter, DateTime.UnixEpoch, DateTime.UnixEpoch, 1),
            ]
        );

        Assert.Equal("AnidbAnimeID,Year,Season\n3,1999,Winter\n20,2001,Fall\n", text);
    }

    [Fact]
    public void Csv_RoundTrips()
    {
        var source = new Dictionary<int, AbstractOverride>
        {
            [3] = new(3, 1999, YearlySeason.Winter, DateTime.UnixEpoch, DateTime.UnixEpoch, null),
            [20] = new(20, 2001, YearlySeason.Fall, DateTime.UnixEpoch, DateTime.UnixEpoch, null),
        };
        var target = new Dictionary<int, AbstractOverride>();

        var text = AnidbStartSeasonOverrideCsv.Export(source.Values);
        var result = AnidbStartSeasonOverrideCsv.Import(new StringReader(text), InMemoryService(target).Object);

        Assert.Equal((2, 0, 0), (result.Added, result.Updated, result.Unchanged));
        Assert.Empty(result.Rejected);
        Assert.Equal(
            source.Values.Select(entry => (entry.AnidbAnimeID, entry.Year, entry.Season)).Order(),
            target.Values.Select(entry => (entry.AnidbAnimeID, entry.Year, entry.Season)).Order()
        );
        Assert.Equal(text, AnidbStartSeasonOverrideCsv.Export(target.Values));
    }

    [Fact]
    public void Csv_Import_ReportsEachLine_AndRemovesNothing()
    {
        var overrides = new Dictionary<int, AbstractOverride>
        {
            [1] = new(1, 2015, YearlySeason.Spring, DateTime.UnixEpoch, DateTime.UnixEpoch, null),
            [2] = new(2, 2015, YearlySeason.Spring, DateTime.UnixEpoch, DateTime.UnixEpoch, null),
            [9] = new(9, 2010, YearlySeason.Fall, DateTime.UnixEpoch, DateTime.UnixEpoch, null),
        };
        var service = InMemoryService(overrides);
        const string Text = "anidbanimeid, year, season\r\n" +
            "# a comment\r\n" +
            "1,2015,spring\r\n" +
            "2,2016,Summer\r\n" +
            "\r\n" +
            "\"3\",\"2017\",\"Autumn\"\r\n" +
            "4,2018\r\n" +
            "0,2018,Fall\r\n" +
            "5,1899,Fall\r\n" +
            "6,2018,Monsoon\r\n" +
            "7,2018,2\r\n" +
            "3,2019,Winter\r\n";

        var result = AnidbStartSeasonOverrideCsv.Import(new StringReader(Text), service.Object);

        Assert.Equal((1, 1, 1), (result.Added, result.Updated, result.Unchanged));
        Assert.Equal([7, 8, 9, 10, 11, 12], result.Rejected.Select(line => line.Line));
        Assert.Equal("4,2018", result.Rejected[0].Text);
        Assert.Contains("on line 6", result.Rejected[5].Reason);
        Assert.Equal((2016, YearlySeason.Summer), (overrides[2].Year, overrides[2].Season));
        Assert.Equal((2017, YearlySeason.Fall), (overrides[3].Year, overrides[3].Season));
        // Left out of the file, so kept.
        Assert.True(overrides.ContainsKey(9));
        service.Verify(entry => entry.SetStartSeasonOverride(1, It.IsAny<int>(), It.IsAny<YearlySeason>()), Times.Never);
    }

    [Fact]
    public void Csv_Import_WithoutAHeader_ReadsTheFirstLine()
    {
        var overrides = new Dictionary<int, AbstractOverride>();

        var result = AnidbStartSeasonOverrideCsv.Import(new StringReader("12,2020,Winter\n"), InMemoryService(overrides).Object);

        Assert.Equal(1, result.Added);
        Assert.True(overrides.ContainsKey(12));
    }

    #endregion
}

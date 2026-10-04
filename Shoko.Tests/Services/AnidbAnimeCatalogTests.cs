using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using Newtonsoft.Json;
using Shoko.Abstractions.Filtering;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Models;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Abstractions.User;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.AniDB;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Extensions;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Server;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

using AniDBExtensions = Shoko.Server.Providers.AniDB.AniDBExtensions;
using CreatorType = Shoko.Server.Providers.AniDB.CreatorType;
using SeasonRules = Shoko.Server.Utilities.SeasonCalendar;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers <see cref="AnidbAnimeCatalog"/>, the listing behind the AniDB anime
/// list and its seasons, and the optional details <see cref="AnidbAnimeDetails"/>
/// adds to the list's models.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class AnidbAnimeCatalogTests
{
    #region Fixtures

    private static AniDB_Anime Anime(
        int id,
        string title,
        PartialDateOnly? airDate,
        AnimeType type = AnimeType.TVSeries,
        bool restricted = false,
        int rating = 0,
        int votes = 0,
        PartialDateOnly? endDate = null
    )
        => new()
        {
            AniDB_AnimeID = id,
            AnimeID = id,
            MainTitle = title,
            AirDate = airDate,
            EndDate = endDate,
            AnimeType = type,
            IsRestricted = restricted,
            Rating = rating,
            VoteCount = votes,
        };

    private static PartialDateOnly Date(int year, int month, int day)
        => new(year, month, day);

    // Regular episodes of an anime, numbered in order, one per date.
    private static IEnumerable<AniDB_Episode> Episodes(int animeID, params DateOnly?[] dates)
        => dates.Select((date, index) => Episode(animeID, index + 1, date));

    private static AniDB_Episode Episode(int animeID, int number, DateOnly? date, EpisodeType type = EpisodeType.Episode)
        => new()
        {
            AniDB_EpisodeID = animeID * 1_000 + (int)type * 100 + number,
            EpisodeID = animeID * 1_000 + (int)type * 100 + number,
            AnimeID = animeID,
            EpisodeNumber = number,
            EpisodeType = type,
            AirDate = date is { } day ? AniDBExtensions.GetAniDBDateAsSeconds(day.ToDateTime(TimeOnly.MinValue)) : null,
        };

    private static DateOnly Day(int year, int month, int day)
        => new(year, month, day);

    // A day well inside a season.
    private static DateOnly MidSeason((int Year, YearlySeason Season) season)
        => new(season.Year, 2 + 3 * (int)season.Season, 1);

    // The ID of the stand-in poster of an anime.
    private static Guid PosterID(int animeID)
        => new(animeID, 0, 0, new byte[8]);

    // The ID of the stand-in backdrop of an anime.
    private static Guid BackdropID(int animeID)
        => new(animeID, 1, 0, new byte[8]);

    private sealed class Harness : IDisposable
    {
        private readonly RepoFactoryScope _scope = new();

        public AnidbAnimeCatalog Catalog { get; }

        public Harness(
            IEnumerable<AniDB_Anime> anime,
            IEnumerable<AniDB_Episode>? episodes = null,
            IEnumerable<AnimeSeries>? series = null,
            IEnumerable<AniDB_Anime_Staff>? staff = null,
            IEnumerable<AniDB_Creator>? creators = null,
            IEnumerable<AniDB_Anime_Tag>? animeTags = null,
            IEnumerable<AniDB_Tag>? tags = null,
            IEnumerable<CrossRef_File_Episode>? fileCrossReferences = null,
            IEnumerable<VideoLocal>? videos = null,
            IReadOnlySet<int>? withPosters = null,
            IReadOnlySet<int>? withBackdrops = null
        )
        {
            var animeTagRepository = CachedRepo.Build<AniDB_Anime_TagRepository, int, AniDB_Anime_Tag>(xref => xref.AniDB_Anime_TagID, animeTags);
            var tagRepository = CachedRepo.Build<AniDB_TagRepository, int, AniDB_Tag>(tag => tag.AniDB_TagID, tags);
            // The anime read their episodes through it for their regular air dates, and the episodes their anime.
            var episodeRepository = CachedRepo.Build<AniDB_EpisodeRepository, int, AniDB_Episode>(episode => episode.AniDB_EpisodeID, episodes);
            var animeRepository = CachedRepo.Build<AniDB_AnimeRepository, int, AniDB_Anime>(entry => entry.AniDB_AnimeID, anime);
            _scope.Set(animeTagRepository).Set(tagRepository).Set(episodeRepository).Set(animeRepository);

            var staffList = staff?.ToList() ?? [];
            var staffRepository = new Mock<AniDB_Anime_StaffRepository>(new object[1]);
            staffRepository
                .Setup(repository => repository.GetStudiosByAnimeIDs(It.IsAny<IReadOnlyCollection<int>>()))
                .Returns((IReadOnlyCollection<int> ids) => [.. staffList.Where(xref => xref.RoleType is CreatorRoleType.Studio && ids.Contains(xref.AnimeID))]);

            var textManager = new Mock<IMetadataTextManager>();
            textManager
                .Setup(manager => manager.GetPreferredTitle(It.IsAny<IWithTitles>()))
                .Returns((IWithTitles entry) => new TitleStub
                {
                    Source = MetadataSource.AniDB,
                    Language = TitleLanguage.English,
                    LanguageCode = "en",
                    Value = entry switch
                    {
                        AnimeSeries shokoSeries => $"Series {shokoSeries.AniDB_ID}",
                        AniDB_Anime anidbAnime => anidbAnime.MainTitle,
                        _ => string.Empty,
                    },
                });
            textManager
                .Setup(manager => manager.GetPreferredOverview(It.IsAny<IWithOverviews>()))
                .Returns((IWithOverviews entry) => new TextStub
                {
                    Source = MetadataSource.AniDB,
                    Language = TitleLanguage.English,
                    LanguageCode = "en",
                    Value = entry is AnimeSeries ? "series overview" : "anime overview",
                });

            Catalog = new ImageCatalog(
                withPosters ?? new HashSet<int>(),
                withBackdrops ?? new HashSet<int>(),
                animeRepository,
                episodeRepository,
                CachedRepo.Build<AnimeSeriesRepository, int, AnimeSeries>(entry => entry.AnimeSeriesID, series),
                staffRepository.Object,
                CachedRepo.Build<AniDB_CreatorRepository, int, AniDB_Creator>(creator => creator.AniDB_CreatorID, creators),
                animeTagRepository,
                tagRepository,
                CachedRepo.Build<CrossRef_File_EpisodeRepository, int, CrossRef_File_Episode>(xref => xref.CrossRef_File_EpisodeID, fileCrossReferences),
                CachedRepo.Build<VideoLocalRepository, int, VideoLocal>(video => video.VideoLocalID, videos),
                textManager.Object
            );
        }

        public int[] IDs(AnidbAnimeListOptions? options = null)
            => [.. Catalog.GetAnime(options).Select(entry => entry.Anime.AnimeID)];

        public void Dispose()
            => _scope.Dispose();
    }

    /// <summary>
    /// A catalog whose anime have a poster or a backdrop only when listed,
    /// identified by <see cref="PosterID"/> and <see cref="BackdropID"/>.
    /// </summary>
    private sealed class ImageCatalog(
        IReadOnlySet<int> withPosters,
        IReadOnlySet<int> withBackdrops,
        AniDB_AnimeRepository animeRepository,
        AniDB_EpisodeRepository episodeRepository,
        AnimeSeriesRepository seriesRepository,
        AniDB_Anime_StaffRepository staffRepository,
        AniDB_CreatorRepository creatorRepository,
        AniDB_Anime_TagRepository animeTagRepository,
        AniDB_TagRepository tagRepository,
        CrossRef_File_EpisodeRepository fileCrossReferenceRepository,
        VideoLocalRepository videoRepository,
        IMetadataTextManager textManager
    ) : AnidbAnimeCatalog(
        animeRepository,
        episodeRepository,
        seriesRepository,
        staffRepository,
        creatorRepository,
        animeTagRepository,
        tagRepository,
        fileCrossReferenceRepository,
        videoRepository,
        textManager
    )
    {
        protected override IImage? GetImage(AniDB_Anime anime, AnimeSeries? series, ImageEntityType type)
            => type switch
            {
                ImageEntityType.Primary when withPosters.Contains(anime.AnimeID) => Mock.Of<IImage>(image => image.ID == PosterID(anime.AnimeID)),
                ImageEntityType.Backdrop when withBackdrops.Contains(anime.AnimeID) => Mock.Of<IImage>(image => image.ID == BackdropID(anime.AnimeID)),
                _ => null,
            };
    }

    private static AniDB_Anime[] PastAnime()
        =>
        [
            Anime(1, "Bravo", Date(2015, 4, 10), rating: 700),
            Anime(2, "Alpha", Date(2015, 7, 5), rating: 900),
            Anime(3, "Charlie", Date(2015, 10, 10), type: AnimeType.Movie, rating: 800),
            Anime(4, "Delta", Date(2016, 1, 15), type: AnimeType.OVA),
            // Its own date says Spring, its episodes say Summer.
            Anime(5, "Echo", Date(2015, 4, 1)),
            // Dated, but its episodes are not.
            Anime(6, "Foxtrot", Date(2015, 5, 12), endDate: Date(2015, 5, 30)),
            Anime(7, "Golf", null),
        ];

    private static AniDB_Episode[] PastEpisodes()
        =>
        [
            // Spring 2015 only.
            .. Episodes(1, Day(2015, 4, 10), Day(2015, 5, 10), Day(2015, 5, 24)),
            // Summer and Fall 2015, a show running across both.
            .. Episodes(2, Day(2015, 7, 5), Day(2015, 8, 10), Day(2015, 10, 20)),
            // Fall 2015.
            .. Episodes(3, Day(2015, 10, 10)),
            // Winter 2016.
            .. Episodes(4, Day(2016, 1, 15)),
            // Summer 2015, its Spring special left out.
            .. Episodes(5, Day(2015, 7, 10)),
            Episode(5, 1, Day(2015, 4, 1), EpisodeType.Special),
            // Undated regular episodes and a dated special: by its own dates, Spring 2015.
            .. Episodes(6, null, null),
            Episode(6, 1, Day(2015, 5, 12), EpisodeType.Special),
        ];

    private static Harness PastHarness(IReadOnlySet<int>? withPosters = null)
        => new(PastAnime(), PastEpisodes(), withPosters: withPosters);

    private static AnidbAnimeListOptions InSeasons(params (int Year, YearlySeason Season)[] seasons)
        => new() { Seasons = seasons };

    private static (int Year, YearlySeason Season) CurrentSeason()
        => SeasonRules.GetYearlySeason(DateTime.Today.ToDateOnly());

    #endregion

    #region Seasons Filter

    [Theory]
    [InlineData(2015, YearlySeason.Spring, new[] { 1, 6 })]
    [InlineData(2015, YearlySeason.Summer, new[] { 2, 5 })]
    [InlineData(2015, YearlySeason.Fall, new[] { 2, 3 })]
    [InlineData(2016, YearlySeason.Winter, new[] { 4 })]
    [InlineData(2014, YearlySeason.Fall, new int[0])]
    public void Seasons_OneSeason_TakesTheAnimeInIt(int year, YearlySeason season, int[] expected)
    {
        using var harness = PastHarness();

        Assert.Equal(expected.Order(), harness.IDs(InSeasons((year, season))).Order());
    }

    [Fact]
    public void Seasons_SeveralSeasons_MatchesAnyOfThem()
    {
        using var harness = PastHarness();

        var ids = harness.IDs(InSeasons((2015, YearlySeason.Spring), (2016, YearlySeason.Winter)));

        Assert.Equal([1, 4, 6], ids.Order());
    }

    [Fact]
    public void Seasons_BeyondTheNextSeason_MatchNothing()
    {
        var next = SeasonRules.GetNextYearlySeason(CurrentSeason());
        var afterNext = SeasonRules.GetNextYearlySeason(next);
        using var harness = new Harness([Anime(1, "Upcoming", null)], Episodes(1, MidSeason(next), MidSeason(afterNext)));

        Assert.Equal([1], harness.IDs(InSeasons(next)));
        Assert.Empty(harness.IDs(InSeasons(afterNext)));
    }

    #endregion

    #region Other Filters

    [Fact]
    public void Types_OnlyTheGivenTypes()
    {
        using var harness = PastHarness();

        Assert.Equal([3, 4], harness.IDs(new() { Types = [AnimeType.Movie, AnimeType.OVA] }).Order());
    }

    [Theory]
    [InlineData(InclusionFilter.True, new[] { 1, 2, 3 })]
    [InlineData(InclusionFilter.Only, new[] { 1 })]
    [InlineData(InclusionFilter.False, new[] { 2, 3 })]
    public void InCollection_SplitsOnTheShokoSeries(InclusionFilter inCollection, int[] expected)
    {
        using var harness = new Harness(
            [Anime(1, "A", Date(2015, 4, 1)), Anime(2, "B", Date(2015, 4, 1)), Anime(3, "C", Date(2015, 4, 1))],
            series: [new AnimeSeries { AnimeSeriesID = 10, AniDB_ID = 1 }]
        );

        Assert.Equal(expected, harness.IDs(new() { InCollection = inCollection }).Order());
    }

    [Theory]
    [InlineData(InclusionFilter.True, new[] { 1, 2, 3 })]
    [InlineData(InclusionFilter.Only, new[] { 2 })]
    [InlineData(InclusionFilter.False, new[] { 1, 3 })]
    public void IncludeMissing_SplitsOnTheSeriesFiles_LeavingAnimeWithoutASeries(InclusionFilter includeMissing, int[] expected)
    {
        using var harness = new Harness(
            [Anime(1, "A", Date(2015, 4, 1)), Anime(2, "B", Date(2015, 4, 1)), Anime(3, "C", Date(2015, 4, 1))],
            series: [new AnimeSeries { AnimeSeriesID = 10, AniDB_ID = 1 }, new AnimeSeries { AnimeSeriesID = 20, AniDB_ID = 2 }],
            fileCrossReferences: [new CrossRef_File_Episode { CrossRef_File_EpisodeID = 1, AnimeID = 1, EpisodeID = 1, Hash = "AAA", FileSize = 1 }],
            videos: [new VideoLocal { VideoLocalID = 1, Hash = "AAA", FileSize = 1 }]
        );

        Assert.Equal(expected, harness.IDs(new() { IncludeMissing = includeMissing }).Order());
    }

    [Theory]
    [InlineData(InclusionFilter.True, new[] { 1, 2 })]
    [InlineData(InclusionFilter.Only, new[] { 2 })]
    [InlineData(InclusionFilter.False, new[] { 1 })]
    public void Restricted_SplitsOnTheRestrictedFlag(InclusionFilter restricted, int[] expected)
    {
        using var harness = new Harness([Anime(1, "A", Date(2015, 4, 1)), Anime(2, "B", Date(2015, 4, 1), restricted: true)]);

        Assert.Equal(expected, harness.IDs(new() { IncludeRestricted = restricted }).Order());
    }

    [Fact]
    public void User_TheirRestrictionsAlwaysApply()
    {
        var user = new Mock<IUser>();
        user.Setup(u => u.IsAllowedToSee(It.IsAny<IAnidbAnime>())).Returns((IAnidbAnime anime) => anime.AnidbID != 2);
        using var harness = new Harness([Anime(1, "A", Date(2015, 4, 1)), Anime(2, "B", Date(2015, 4, 1))]);

        Assert.Equal([1], harness.IDs(new() { User = user.Object }));
    }

    [Fact]
    public void TitlePrefix_MatchesThePreferredTitleIgnoringCase()
    {
        using var harness = new Harness(
            [Anime(1, "Alpha", Date(2015, 4, 1)), Anime(2, "Beta", Date(2015, 4, 1))],
            series: [new AnimeSeries { AnimeSeriesID = 10, AniDB_ID = 2 }]
        );

        Assert.Equal([1], harness.IDs(new() { TitlePrefix = "alp" }));
        // A series in the collection goes by its series' title.
        Assert.Equal([2], harness.IDs(new() { TitlePrefix = "series" }));
    }

    #endregion

    #region Ordering

    [Fact]
    public void OrderBy_DefaultsToTitleWithoutSeasons()
    {
        using var harness = PastHarness();

        Assert.Equal([2, 1, 3, 4, 5, 6, 7], harness.IDs());
    }

    [Fact]
    public void OrderBy_DefaultsToAirDateWithSeasons_ThenTitle()
    {
        using var harness = new Harness(
            [
                Anime(1, "Zulu", Date(2015, 10, 10)),
                Anime(2, "Yankee", Date(2015, 10, 1)),
                Anime(3, "Alpha", Date(2015, 10, 10)),
            ],
            [.. Episodes(1, Day(2015, 10, 10)), .. Episodes(2, Day(2015, 10, 1)), .. Episodes(3, Day(2015, 10, 10))]
        );

        Assert.Equal([2, 3, 1], harness.IDs(InSeasons((2015, YearlySeason.Fall))));
    }

    [Fact]
    public void OrderBy_AirDate_PutsUnknownDatesLast()
    {
        using var harness = PastHarness();

        Assert.Equal([5, 1, 6, 2, 3, 4, 7], harness.IDs(new() { OrderBy = AnidbAnimeListOrder.AirDate }));
    }

    [Fact]
    public void OrderBy_Rating_HighestFirst()
    {
        using var harness = PastHarness();

        Assert.Equal([2, 3, 1], harness.IDs(new() { OrderBy = AnidbAnimeListOrder.Rating }).Take(3));
    }

    #endregion

    #region Seasons Listing

    [Fact]
    public void GetSeasons_CountsEachSeasonNewestFirst_AsTheListDoes()
    {
        using var harness = PastHarness();

        var seasons = harness.Catalog.GetSeasons()
            .Where(season => season.Year < 2017)
            .Select(season => (season.Year, season.Season, season.Count))
            .ToList();

        Assert.Equal(
            [
                (2016, YearlySeason.Winter, 1),
                (2015, YearlySeason.Fall, 2),
                (2015, YearlySeason.Summer, 2),
                (2015, YearlySeason.Spring, 2),
            ],
            seasons
        );
        foreach (var (year, season, count) in seasons)
            Assert.Equal(count, harness.IDs(InSeasons((year, season))).Length);
    }

    [Fact]
    public void GetSeasons_AnimeWithoutDatedRegularEpisodes_GoesByItsOwnDates()
    {
        using var harness = new Harness([PastAnime()[5], PastAnime()[6]], PastEpisodes().Where(episode => episode.AnimeID == 6));

        var seasons = harness.Catalog.GetSeasons().Where(season => season.Count > 0).Select(season => (season.Year, season.Season, season.Count));

        Assert.Equal([(2015, YearlySeason.Spring, 1)], seasons);
    }

    [Fact]
    public void GetSeasons_AnimeBefore1970_GoesByItsOwnDates()
    {
        var anime = Anime(1, "Old", Date(1965, 4, 20));
        anime.EndDate = Date(1965, 8, 1);
        using var harness = new Harness([anime], Episodes(1, null, null));

        var seasons = harness.Catalog.GetSeasons().Where(season => season.Count > 0).Select(season => (season.Year, season.Season));

        Assert.Equal([(1965, YearlySeason.Summer), (1965, YearlySeason.Spring)], seasons);
    }

    [Fact]
    public void GetSeasons_APlaceholderTheAnimesDateStandsIn_CountsAsDated()
    {
        // A single episode on 1970-01-01 is stored as AniDB's placeholder, 0.
        using var harness = new Harness([Anime(1, "Epoch", Date(1970, 1, 1))], Episodes(1, Day(1970, 1, 1)));

        var seasons = harness.Catalog.GetSeasons().Where(season => season.Count > 0).Select(season => (season.Year, season.Season));

        Assert.Equal([SeasonRules.GetYearlySeason(Day(1970, 1, 1))], seasons);
    }

    [Fact]
    public void GetSeasons_ListsAtMostOneSeasonAhead()
    {
        var current = CurrentSeason();
        var next = SeasonRules.GetNextYearlySeason(current);
        var afterNext = SeasonRules.GetNextYearlySeason(next);
        using var harness = new Harness(
            [Anime(1, "Running", null), Anime(2, "Later", null)],
            [.. Episodes(1, MidSeason(current), MidSeason(next), MidSeason(afterNext)), .. Episodes(2, MidSeason(afterNext))]
        );

        var seasons = harness.Catalog.GetSeasons().Select(season => (season.Year, season.Season, season.Count));

        Assert.Equal([(next.Year, next.Season, 1), (current.Year, current.Season, 1)], seasons);
    }

    [Fact]
    public void GetSeasons_AppliesTheOtherFilters()
    {
        using var harness = PastHarness();

        var seasons = harness.Catalog.GetSeasons(new() { Types = [AnimeType.Movie] }).Where(season => season.Count > 0);

        Assert.Equal([(2015, YearlySeason.Fall)], seasons.Select(season => (season.Year, season.Season)));
    }

    #endregion

    #region Season Images

    // The images a season gets, as poster and backdrop IDs.
    private static (Guid? Poster, Guid? Backdrop) ImagesOf(
        Harness harness,
        (int Year, YearlySeason Season) season,
        AnidbAnimeListOptions? options = null
    )
    {
        var entry = harness.Catalog.GetSeasons(options, includeImages: true).Single(entry => (entry.Year, entry.Season) == season);
        return (entry.Poster?.ID, entry.Backdrop?.ID);
    }

    [Fact]
    public void GetSeasons_Images_OnlyWhenAsked()
    {
        var all = new HashSet<int> { 1, 2, 3, 4, 5 };
        using var harness = new Harness(PastAnime(), PastEpisodes(), withPosters: all, withBackdrops: all);

        Assert.All(harness.Catalog.GetSeasons(), season => Assert.True(season is { Poster: null, Backdrop: null }));
        Assert.All(harness.Catalog.GetSeasons(includeImages: true).Where(season => season.Count > 0), season => Assert.NotNull(season.Poster));
    }

    [Theory]
    [InlineData(new[] { 1, 2, 3 }, 2)]
    [InlineData(new[] { 1, 3 }, 1)]
    [InlineData(new[] { 3 }, 3)]
    public void GetSeasons_Images_FromStartersFirst_ThenTheNewestCarryOvers(int[] withPosters, int expected)
    {
        AniDB_Anime[] anime =
        [
            Anime(1, "Carried Over", null, rating: 700, votes: 1_000),
            Anime(2, "New", null, rating: 500, votes: 10),
            Anime(3, "Carried Over Longer", null, rating: 900, votes: 10_000),
        ];
        AniDB_Episode[] episodes =
        [
            .. Episodes(1, Day(2015, 7, 5), Day(2015, 10, 20)),
            .. Episodes(2, Day(2015, 10, 10)),
            .. Episodes(3, Day(2015, 4, 10), Day(2015, 7, 10), Day(2015, 10, 15)),
        ];
        using var harness = new Harness(anime, episodes, withPosters: withPosters.ToHashSet());

        Assert.Equal(PosterID(expected), ImagesOf(harness, (2015, YearlySeason.Fall)).Poster);
    }

    [Fact]
    public void GetSeasons_Images_RankByWeightedRating()
    {
        // Neither the highest rating on few votes nor the most votes on a poor
        // rating beats a solid rating on many votes.
        AniDB_Anime[] anime =
        [
            Anime(1, "Few Votes", null, rating: 1_000, votes: 5),
            Anime(2, "Solid", null, rating: 880, votes: 2_000),
            Anime(3, "Middling", null, rating: 600, votes: 100),
            Anime(4, "Most Votes", null, rating: 500, votes: 3_000),
        ];
        AniDB_Episode[] episodes =
        [
            .. Episodes(1, Day(2015, 4, 10)),
            .. Episodes(2, Day(2015, 4, 20)),
            .. Episodes(3, Day(2015, 4, 5)),
            .. Episodes(4, Day(2015, 4, 5)),
        ];
        using var harness = new Harness(anime, episodes, withPosters: new HashSet<int> { 1, 2, 3, 4 });

        Assert.Equal(PosterID(2), ImagesOf(harness, (2015, YearlySeason.Spring)).Poster);
    }

    [Theory]
    [InlineData(new[] { 1, 2, 3 }, 2)]
    [InlineData(new[] { 1, 3 }, 3)]
    [InlineData(new[] { 1 }, 1)]
    public void GetSeasons_Images_TiesGoToTheEarlierStartThenTheLowerID(int[] withPosters, int expected)
    {
        // No votes, as in a season yet to air.
        AniDB_Anime[] anime = [Anime(1, "Later", null), Anime(2, "Early", null), Anime(3, "Early Too", null)];
        AniDB_Episode[] episodes = [.. Episodes(1, Day(2015, 5, 10)), .. Episodes(2, Day(2015, 4, 10)), .. Episodes(3, Day(2015, 4, 10))];
        using var harness = new Harness(anime, episodes, withPosters: withPosters.ToHashSet());

        Assert.Equal(PosterID(expected), ImagesOf(harness, (2015, YearlySeason.Spring)).Poster);
    }

    [Fact]
    public void GetSeasons_Images_BackdropOfTheAnimeThePosterComesFrom()
    {
        AniDB_Anime[] anime = [Anime(1, "Top", null, rating: 900, votes: 1_000), Anime(2, "Second", null, rating: 500, votes: 1_000)];
        AniDB_Episode[] episodes = [.. Episodes(1, Day(2015, 4, 10)), .. Episodes(2, Day(2015, 4, 10))];

        using (var harness = new Harness(anime, episodes, withPosters: new HashSet<int> { 1, 2 }, withBackdrops: new HashSet<int> { 1, 2 }))
            Assert.Equal(((Guid?)PosterID(1), (Guid?)BackdropID(1)), ImagesOf(harness, (2015, YearlySeason.Spring)));

        using (var harness = new Harness(anime, episodes, withPosters: new HashSet<int> { 1, 2 }, withBackdrops: new HashSet<int> { 2 }))
            Assert.Equal(((Guid?)PosterID(1), (Guid?)null), ImagesOf(harness, (2015, YearlySeason.Spring)));
    }

    [Fact]
    public void GetSeasons_Images_LeaveOutFilteredAnime()
    {
        AniDB_Anime[] anime =
        [
            Anime(1, "Open", null, rating: 100, votes: 10),
            Anime(2, "Restricted", null, restricted: true, rating: 900, votes: 1_000),
        ];
        using var harness = new Harness(
            anime,
            [.. Episodes(1, Day(2015, 4, 10)), .. Episodes(2, Day(2015, 4, 10))],
            withPosters: new HashSet<int> { 1, 2 }
        );

        Assert.Equal(PosterID(1), ImagesOf(harness, (2015, YearlySeason.Spring), new() { IncludeRestricted = InclusionFilter.False }).Poster);
    }

    #endregion

    #region Details

    private static Harness DetailsHarness()
        => new(
            [Anime(1, "Alpha", Date(2015, 4, 1)), Anime(2, "Beta", Date(2015, 4, 1))],
            // Starts in Spring, its Winter special left out; Beta has no episodes.
            [.. Episodes(1, Day(2015, 4, 1), Day(2015, 7, 1)), Episode(1, 1, Day(2015, 1, 10), EpisodeType.Special)],
            series: [new AnimeSeries { AnimeSeriesID = 10, AniDB_ID = 1 }],
            staff:
            [
                new AniDB_Anime_Staff { AniDB_Anime_StaffID = 1, AnimeID = 1, CreatorID = 100, RoleType = CreatorRoleType.Studio, Role = "Animation Work", Ordering = 2 },
                new AniDB_Anime_Staff { AniDB_Anime_StaffID = 2, AnimeID = 1, CreatorID = 101, RoleType = CreatorRoleType.Studio, Role = "Animation Work", Ordering = 1 },
                new AniDB_Anime_Staff { AniDB_Anime_StaffID = 3, AnimeID = 1, CreatorID = 102, RoleType = CreatorRoleType.Director, Role = "Direction", Ordering = 0 },
            ],
            creators:
            [
                new AniDB_Creator { AniDB_CreatorID = 1, CreatorID = 100, Name = "Studio One", Type = CreatorType.Company },
                new AniDB_Creator { AniDB_CreatorID = 2, CreatorID = 101, Name = "Studio Two", Type = CreatorType.Company },
                new AniDB_Creator { AniDB_CreatorID = 3, CreatorID = 102, Name = "A Director", Type = CreatorType.Person },
            ],
            animeTags:
            [
                new AniDB_Anime_Tag { AniDB_Anime_TagID = 1, AnimeID = 1, TagID = 1, Weight = 300 },
                new AniDB_Anime_Tag { AniDB_Anime_TagID = 2, AnimeID = 1, TagID = 2, Weight = 600 },
                new AniDB_Anime_Tag { AniDB_Anime_TagID = 3, AnimeID = 1, TagID = 3, Weight = 500 },
                new AniDB_Anime_Tag { AniDB_Anime_TagID = 4, AnimeID = 1, TagID = 4, Weight = 600, LocalSpoiler = true },
                new AniDB_Anime_Tag { AniDB_Anime_TagID = 5, AnimeID = 1, TagID = 5, Weight = 600 },
                new AniDB_Anime_Tag { AniDB_Anime_TagID = 6, AnimeID = 1, TagID = 6, Weight = 200 },
                new AniDB_Anime_Tag { AniDB_Anime_TagID = 7, AnimeID = 1, TagID = 2798, Weight = 0 },
            ],
            tags:
            [
                new AniDB_Tag { AniDB_TagID = 1, TagID = 1, TagNameSource = "comedy", Verified = true },
                new AniDB_Tag { AniDB_TagID = 2, TagID = 2, TagNameSource = "action", Verified = true },
                // Not a genre.
                new AniDB_Tag { AniDB_TagID = 3, TagID = 3, TagNameSource = "school", Verified = true },
                // A genre, but a spoiler for this anime.
                new AniDB_Tag { AniDB_TagID = 4, TagID = 4, TagNameSource = "romance", Verified = true },
                // A genre, but a spoiler for every anime.
                new AniDB_Tag { AniDB_TagID = 5, TagID = 5, TagNameSource = "tragedy", Verified = true, GlobalSpoiler = true },
                new AniDB_Tag { AniDB_TagID = 6, TagID = 6, TagNameSource = "fantasy", Verified = true },
                new AniDB_Tag { AniDB_TagID = 7, TagID = 2798, TagNameSource = "manga", Verified = true },
            ],
            fileCrossReferences:
            [
                // One file covering two episodes counts once.
                new CrossRef_File_Episode { CrossRef_File_EpisodeID = 1, AnimeID = 1, EpisodeID = 1, Hash = "AAA", FileSize = 1 },
                new CrossRef_File_Episode { CrossRef_File_EpisodeID = 2, AnimeID = 1, EpisodeID = 2, Hash = "AAA", FileSize = 1 },
                new CrossRef_File_Episode { CrossRef_File_EpisodeID = 3, AnimeID = 1, EpisodeID = 3, Hash = "BBB", FileSize = 2 },
                // Linked, but not on disk.
                new CrossRef_File_Episode { CrossRef_File_EpisodeID = 4, AnimeID = 1, EpisodeID = 4, Hash = "CCC", FileSize = 3 },
            ],
            videos:
            [
                new VideoLocal { VideoLocalID = 1, Hash = "AAA", FileSize = 1 },
                new VideoLocal { VideoLocalID = 2, Hash = "BBB", FileSize = 2 },
            ]
        );

    private static AnidbAnime Apply(Harness harness, int animeID, int tagLimit, params AnidbAnime.IncludeDetails[] include)
    {
        var entries = harness.Catalog.GetAnime();
        var (anime, series) = entries.Single(entry => entry.Anime.AnimeID == animeID);
        var set = include.ToHashSet();
        var model = new AnidbAnime();
        AnidbAnimeDetails.Apply(model, anime, series, set, tagLimit, harness.Catalog, AnidbAnimeDetails.LoadStudios(harness.Catalog, entries, set));
        return model;
    }

    [Fact]
    public void Details_NoneAsked_LeavesEveryFieldUnset()
    {
        using var harness = DetailsHarness();

        var model = Apply(harness, 1, 5);

        Assert.Null(model.Overview);
        Assert.Null(model.Studios);
        Assert.Null(model.SourceMaterial);
        Assert.Null(model.Tags);
        Assert.Null(model.Files);
    }

    [Fact]
    public void Details_EachAsked_SetsOnlyThatField()
    {
        using var harness = DetailsHarness();

        foreach (var detail in Enum.GetValues<AnidbAnime.IncludeDetails>())
        {
            var model = Apply(harness, 1, 5, detail);

            Assert.Equal(detail is AnidbAnime.IncludeDetails.Overview, model.Overview is not null);
            Assert.Equal(detail is AnidbAnime.IncludeDetails.Studios, model.Studios is not null);
            Assert.Equal(detail is AnidbAnime.IncludeDetails.SourceMaterial, model.SourceMaterial is not null);
            Assert.Equal(detail is AnidbAnime.IncludeDetails.Tags, model.Tags is not null);
            Assert.Equal(detail is AnidbAnime.IncludeDetails.Files, model.Files is not null);
            Assert.Equal(detail is AnidbAnime.IncludeDetails.StartSeason, model.ShouldSerializeStartSeason());
            Assert.Equal(detail is AnidbAnime.IncludeDetails.EpisodeDuration, model.ShouldSerializeEpisodeDuration());
        }
    }

    [Fact]
    public void Details_AllAsked_FillsEveryField()
    {
        using var harness = DetailsHarness();

        var model = Apply(harness, 1, 5, Enum.GetValues<AnidbAnime.IncludeDetails>());

        Assert.Equal("series overview", model.Overview);
        Assert.Equal(["Studio Two", "Studio One"], model.Studios!.Select(studio => studio.Name));
        Assert.Equal(SourceMaterial.Manga, model.SourceMaterial);
        Assert.Equal(["action", "comedy", "fantasy"], model.Tags!.Select(tag => tag.Name));
        Assert.Equal(2, model.Files!.VideoCount);
        Assert.Equal(new SeasonWithYear(2015, YearlySeason.Spring), model.StartSeason);
    }

    [Fact]
    public void Details_EpisodeDuration_IsTheMedianKnownRegularLength()
    {
        var episodes = Episodes(1, Day(2015, 4, 1), Day(2015, 4, 8), Day(2015, 4, 15)).ToList();
        episodes[0].LengthSeconds = 1440;
        episodes[1].LengthSeconds = 1500;
        var special = Episode(1, 1, Day(2015, 4, 2), EpisodeType.Special);
        special.LengthSeconds = 300;
        using var harness = new Harness([Anime(1, "Alpha", Date(2015, 4, 1))], [.. episodes, special]);

        Assert.Equal(TimeSpan.FromSeconds(1470), Apply(harness, 1, 5, AnidbAnime.IncludeDetails.EpisodeDuration).EpisodeDuration);
    }

    [Fact]
    public void Details_StartSeason_SentAsNullWithoutDates_AndOnlyWhenAsked()
    {
        using (var undated = new Harness([Anime(1, "Alpha", null)]))
            Assert.Contains("\"StartSeason\":null", JsonConvert.SerializeObject(Apply(undated, 1, 5, AnidbAnime.IncludeDetails.StartSeason)));

        using var harness = DetailsHarness();
        Assert.DoesNotContain("StartSeason", JsonConvert.SerializeObject(Apply(harness, 1, 5)));
    }

    [Fact]
    public void Details_AnimeOutsideTheCollection_HasNoFilesOrStudiosButStillTheFields()
    {
        using var harness = DetailsHarness();

        var model = Apply(harness, 2, 5, AnidbAnime.IncludeDetails.Files, AnidbAnime.IncludeDetails.Studios, AnidbAnime.IncludeDetails.Overview);

        Assert.Equal(0, model.Files!.VideoCount);
        Assert.Empty(model.Studios!);
        Assert.Equal("anime overview", model.Overview);
    }

    [Fact]
    public void Details_TagLimit_KeepsTheHeaviest()
    {
        using var harness = DetailsHarness();

        Assert.Equal(["action"], Apply(harness, 1, 1, AnidbAnime.IncludeDetails.Tags).Tags!.Select(tag => tag.Name));
        Assert.Empty(Apply(harness, 1, 0, AnidbAnime.IncludeDetails.Tags).Tags!);
    }

    #endregion
}

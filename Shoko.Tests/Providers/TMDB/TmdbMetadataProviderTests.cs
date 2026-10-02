using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Matching;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Search;
using Shoko.Server.Filters;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.TMDB;
using Shoko.Server.Repositories.Direct.TMDB.Optional;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Tests.Infrastructure;
using TMDbLib.Objects.Exceptions;
using TMDbLib.Objects.Search;
using Xunit;

using TmdbImage = TMDbLib.Objects.General.ImageData;

namespace Shoko.Tests.Providers.TMDB;

/// <summary>
/// Covers what the TMDB provider hands the core: its image candidates, its
/// lookups, the candidates its episode matching offers, and its pause status.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class TmdbMetadataProviderTests
{
    #region Images

    [Fact]
    public void TheCandidatesKeepTmdbsOrderAndStoredPaths()
    {
        var candidates = TmdbMetadataProvider.ToCandidates(
            [
                new TmdbImage { FilePath = "/second.jpg", Width = 500, Height = 750, Iso_639_1 = "ja", Iso_3166_1 = "JP", VoteAverage = 5.3, VoteCount = 4 },
                new TmdbImage { FilePath = null },
                new TmdbImage { FilePath = "/logo.svg", Iso_639_1 = "en" },
                new TmdbImage { FilePath = "/first.jpg" },
            ],
            ImageEntityType.Primary
        );

        Assert.Equal(["second.jpg", "logo.png", "first.jpg"], candidates.ConvertAll(candidate => candidate.ResourceID));
        Assert.All(candidates, candidate => Assert.Equal(ImageEntityType.Primary, candidate.ImageType));
        // TMDB's own downloads never counted the entry's default apart from
        // the rest, so none is marked.
        Assert.All(candidates, candidate => Assert.False(candidate.IsDefault));
        Assert.Equal(("ja", "JP", 500, 750), (candidates[0].LanguageCode, candidates[0].CountryCode, candidates[0].Width, candidates[0].Height));
        Assert.Equal((5.3, 4), (candidates[0].Rating, candidates[0].RatingVotes));
    }

    [Fact]
    public void ARatingIsOnlyPassedOnWithVotesBehindIt()
    {
        var candidates = TmdbMetadataProvider.ToCandidates(
            [
                new TmdbImage { FilePath = "/unrated.jpg", VoteAverage = 0, VoteCount = 0 },
                new TmdbImage { FilePath = "/unvoted.jpg", VoteAverage = 7, VoteCount = 0 },
                new TmdbImage { FilePath = "/low.jpg", VoteAverage = 0.5, VoteCount = 3 },
            ],
            ImageEntityType.Backdrop
        );

        Assert.All(candidates, candidate => Assert.Null(candidate.Rating));
        Assert.All(candidates, candidate => Assert.Null(candidate.RatingVotes));
        Assert.All(candidates, candidate => Assert.False(candidate.IsDefault));
    }

    [Fact]
    public void NoImagesMakeNoCandidates()
        => Assert.Empty(TmdbMetadataProvider.ToCandidates(null, ImageEntityType.Logo));

    #endregion

    #region Lookup and Preview

    private static TmdbMetadataProvider Provider(TMDB_Show[]? shows = null, TMDB_Movie[]? movies = null)
        => new(
            NullLogger<TmdbMetadataProvider>.Instance,
            null!,
            null!,
            new TmdbRateLimiter(NullLogger<TmdbRateLimiter>.Instance, SettingsProvider(), TimeSpan.FromSeconds(10)),
            null!,
            null!,
            null!,
            null!,
            CachedRepo.Build<TMDB_ShowRepository, int, TMDB_Show>(show => show.TmdbShowID, shows ?? []),
            null!,
            null!,
            CachedRepo.Build<TMDB_MovieRepository, int, TMDB_Movie>(movie => movie.TmdbMovieID, movies ?? []),
            null!,
            null!
        );

    [Fact]
    public async Task AStoredShowIsLookedUpWithoutAskingTmdb()
    {
        var provider = Provider(shows: [
            new TMDB_Show(5) { EnglishTitle = "Show" },
        ]);

        // The client is missing, so asking TMDB would throw.
        var result = await provider.LookupSeries(new(MetadataSource.TMDB, MetadataEntityType.Series, "5"), TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Series, "5"), result.ID);
        Assert.Equal("Show", result.Title);
        // No path, so no URL, and the image host is never asked for.
        Assert.Null(result.PosterUrl);
        Assert.Null(result.BackdropUrl);
    }

    [Fact]
    public async Task AStoredMovieIsLookedUpWithoutAskingTmdb()
    {
        var provider = Provider(movies: [new TMDB_Movie(600) { EnglishTitle = "Film", IsRestricted = true, IsVideo = true, ReleasedAt = new DateOnly(2001, 7, 20) }]);

        var result = await provider.LookupMovie(new(MetadataSource.TMDB, MetadataEntityType.Movie, "600"), TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("Film", result.Title);
        Assert.True(result.IsRestricted);
        Assert.True(result.IsStandaloneVideo);
        Assert.Equal(new PartialDateOnly(2001, 7, 20), result.ReleasedAt);
    }

    [Fact]
    public async Task AnIDOfAnotherKindOrSourceIsNotLookedUp()
    {
        var provider = Provider(shows: [new TMDB_Show(5)], movies: [new TMDB_Movie(5)]);

        Assert.Null(await provider.LookupSeries(new(MetadataSource.TMDB, MetadataEntityType.Movie, "5"), TestContext.Current.CancellationToken));
        Assert.Null(await provider.LookupMovie(new(MetadataSource.AniDB, MetadataEntityType.Movie, "5"), TestContext.Current.CancellationToken));
        Assert.Null(await provider.LookupSeries(new(MetadataSource.TMDB, MetadataEntityType.Series, "0"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void AMovieMatchIsACandidateForTheEpisodeItStandsFor()
    {
        var anime = new AniDB_Anime { AnimeID = 30 };
        var episode = new AniDB_Episode { EpisodeID = 300, AnimeID = 30 };
        var match = new TmdbAutoSearchResult(anime, episode, new SearchMovie { Id = 600, Title = "Film", Adult = true, Video = false }, MatchRating.TitleMatches)
        {
            IsRemote = true,
        };

        var candidate = TmdbMetadataProvider.ToCandidate(match);

        Assert.Equal(new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Movie, "600"), candidate.ID);
        var movie = Assert.IsType<MetadataMovieSearchResult>(candidate.Result);
        Assert.True(movie.IsRestricted);
        Assert.Equal((30, 300), (candidate.AnidbAnimeID, candidate.AnidbEpisodeID));
        Assert.Equal(MatchRating.TitleMatches, candidate.MatchRating);
        Assert.True(candidate.IsRemote);
        Assert.False(candidate.IsLocal);
    }

    [Fact]
    public void AShowMatchIsACandidateForTheWholeAnime()
    {
        var match = new TmdbAutoSearchResult(new AniDB_Anime { AnimeID = 30 }, new SearchTv { Id = 5, Name = "Show", FirstAirDate = new DateTime(2020, 4, 5) });

        var candidate = TmdbMetadataProvider.ToCandidate(match);

        var show = Assert.IsType<MetadataSeriesSearchResult>(candidate.Result);
        Assert.Equal("Show", show.Title);
        Assert.Equal(new PartialDateOnly(2020, 4, 5), show.FirstAiredAt);
        Assert.Equal(30, candidate.AnidbAnimeID);
        Assert.Null(candidate.AnidbEpisodeID);
    }

    // A prequel's link is listed for context, rated nothing for this anime,
    // its own rating kept apart, and never taken.
    [Fact]
    public void APrequelsLinkIsContextOnly()
    {
        var anime = new AniDB_Anime { AnimeID = 15067 };
        var prequel = new AniDB_Anime { AnimeID = 1 };
        var show = new TMDB_Show(60572) { EnglishTitle = "Pokemon", OriginalTitle = "ポケットモンスター" };

        var candidate = TmdbMetadataProvider.ToCandidate(TmdbSearchService.PrequelLink(anime, prequel, MatchRating.UserVerified, show));

        Assert.Equal(new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Series, "60572"), candidate.ID);
        Assert.Equal((MetadataAutoLinkOrigin.PrequelLink, MatchRating.None, MatchRating.UserVerified, 1), (candidate.Origin, candidate.MatchRating, candidate.LinkMatchRating, candidate.PrequelAnidbAnimeID));
        Assert.Equal(MatchRejectionReason.ExistingLink, candidate.Rejection?.Reason);
        Assert.True(candidate.IsLocal);
    }

    [Fact]
    public void ARejectedMatchIsACandidateCarryingWhy()
    {
        var scored = new MetadataSeriesSearchResult { ID = new(MetadataSource.TMDB, MetadataEntityType.Series, "5"), Title = "Show (fetched)", EpisodeCount = 12 };
        var match = new TmdbAutoSearchResult(new AniDB_Anime { AnimeID = 30 }, new SearchTv { Id = 5, Name = "Show" }, MatchRating.TitleKindaMatches)
        {
            IsRemote = true,
            Candidate = scored,
            Rejection = new() { Reason = MatchRejectionReason.Outranked, Details = "Searched for \"Show\"." },
        };

        var candidate = TmdbMetadataProvider.ToCandidate(match);

        Assert.Same(scored, candidate.Result);
        Assert.Equal(MatchRejectionReason.Outranked, candidate.Rejection?.Reason);
        Assert.Equal("Searched for \"Show\".", candidate.Rejection?.Details);
        Assert.Equal(MatchRating.TitleKindaMatches, candidate.MatchRating);
    }

    #endregion

    #region Episode Matching

    private static readonly DateOnly _firstAired = new(2024, 1, 7);

    // One show with two regular seasons and a specials season.
    private static TMDB_EpisodeRepository MatchingEpisodes()
        => CachedRepo.Build<TMDB_EpisodeRepository, int, TMDB_Episode>(episode => episode.TmdbEpisodeID,
        [
            new TMDB_Episode { TmdbEpisodeID = 500, TmdbShowID = 5, TmdbSeasonID = 50, SeasonNumber = 0, EpisodeNumber = 1, AiredAt = _firstAired.AddDays(3) },
            new TMDB_Episode { TmdbEpisodeID = 511, TmdbShowID = 5, TmdbSeasonID = 51, SeasonNumber = 1, EpisodeNumber = 1, AiredAt = _firstAired.AddYears(-1) },
            new TMDB_Episode { TmdbEpisodeID = 521, TmdbShowID = 5, TmdbSeasonID = 52, SeasonNumber = 2, EpisodeNumber = 1, AiredAt = _firstAired },
        ]);

    // The shows the matching seasons and episodes belong to, which a season
    // or episode needs to be found.
    private static TMDB_ShowRepository MatchingShows()
        => CachedRepo.Build<TMDB_ShowRepository, int, TMDB_Show>(show => show.TmdbShowID, [new TMDB_Show(5), new TMDB_Show(6)]);

    private static TmdbMetadataProvider MatchingProvider(
        TMDB_EpisodeRepository? episodes = null,
        TMDB_AlternateOrdering_SeasonRepository? alternateOrderingSeasons = null,
        CrossRef_AniDB_TMDB_EpisodeRepository? episodeLinks = null
    )
        => new(
            NullLogger<TmdbMetadataProvider>.Instance,
            null!,
            null!,
            new TmdbRateLimiter(NullLogger<TmdbRateLimiter>.Instance, SettingsProvider(), TimeSpan.FromSeconds(10)),
            null!,
            new MetadataMatchingEngine(NullLogger<MetadataMatchingEngine>.Instance, new FuzzySearchService()),
            null!,
            null!,
            MatchingShows(),
            CachedRepo.Build<TMDB_SeasonRepository, int, TMDB_Season>(season => season.TmdbSeasonID,
            [
                new TMDB_Season { TmdbSeasonID = 50, TmdbShowID = 5, SeasonNumber = 0 },
                new TMDB_Season { TmdbSeasonID = 51, TmdbShowID = 5, SeasonNumber = 1 },
                new TMDB_Season { TmdbSeasonID = 52, TmdbShowID = 5, SeasonNumber = 2 },
                new TMDB_Season { TmdbSeasonID = 61, TmdbShowID = 6, SeasonNumber = 1 },
            ]),
            episodes ?? MatchingEpisodes(),
            null!,
            alternateOrderingSeasons!,
            episodeLinks!
        );

    private static IAnidbEpisode AnidbEpisode(int id, int number, DateOnly? airDate, EpisodeType type = EpisodeType.Episode)
    {
        var mock = new Mock<IAnidbEpisode>();
        mock.SetupGet(episode => episode.AnidbID).Returns(id);
        mock.SetupGet(episode => episode.AnidbAnimeID).Returns(30);
        mock.SetupGet(episode => episode.Type).Returns(type);
        mock.SetupGet(episode => episode.EpisodeNumber).Returns(number);
        mock.SetupGet(episode => episode.AirDate).Returns(airDate);
        mock.SetupGet(episode => episode.RegularAirDate).Returns(airDate);
        mock.SetupGet(episode => episode.Titles).Returns([]);
        mock.SetupGet(episode => episode.ShokoEpisodes).Returns([]);
        return mock.Object;
    }

    private static IMetadataEpisodeCrossReference Link(int anidbEpisodeID, int showID, int episodeID, MatchRating rating)
    {
        var mock = new Mock<IMetadataEpisodeCrossReference>();
        mock.SetupGet(link => link.AnidbEpisodeID).Returns(anidbEpisodeID);
        mock.SetupGet(link => link.Source).Returns(MetadataSource.TMDB);
        mock.SetupGet(link => link.ProviderParentID).Returns(new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Series, showID.ToString()));
        mock.SetupGet(link => link.ProviderID).Returns(new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Episode, episodeID.ToString()));
        mock.SetupGet(link => link.MatchRating).Returns(rating);
        return mock.Object;
    }

    private static Task<IReadOnlyList<EpisodeMatch>> Match(
        TmdbMetadataProvider provider,
        IReadOnlyList<IAnidbEpisode> episodes,
        object? seasonID = null,
        IReadOnlyList<IMetadataEpisodeCrossReference>? existing = null,
        bool considerOtherLinks = false
    )
    {
        var anime = new Mock<IAnidbAnime>();
        anime.SetupGet(anime => anime.AnidbID).Returns(30);
        return provider.MatchEpisodes(
            anime.Object,
            episodes,
            new(MetadataSource.TMDB, MetadataEntityType.Series, "5"),
            seasonID is null ? null : new(MetadataSource.TMDB, MetadataEntityType.Season, seasonID.ToString()!),
            existing,
            considerOtherLinks,
            TestContext.Current.CancellationToken
        );
    }

    // One season is matched with the show's specials beside it, and nothing
    // from the other seasons, however well it lines up.
    [Fact]
    public async Task MatchingOneSeason_OffersItsEpisodesAndTheSpecials()
    {
        using var scope = new RepoFactoryScope().Set(MatchingShows());
        var provider = MatchingProvider();

        var matches = await Match(provider, [AnidbEpisode(1, 1, _firstAired), AnidbEpisode(2, 1, _firstAired.AddDays(3), EpisodeType.Special)], seasonID: 51);

        Assert.Equal("511", Assert.Single(matches, match => match.AnidbEpisode.AnidbID is 1).Candidate?.ID.ID);
        Assert.Equal("500", Assert.Single(matches, match => match.AnidbEpisode.AnidbID is 2).Candidate?.ID.ID);
    }

    // A season of another show is refused rather than widened to the whole
    // show.
    [Fact]
    public async Task ASeasonOfAnotherShow_IsRefused()
    {
        using var scope = new RepoFactoryScope().Set(MatchingShows());
        var matches = await Match(MatchingProvider(), [AnidbEpisode(1, 1, _firstAired)], seasonID: 61);

        Assert.Empty(matches);
    }

    // A link to an episode the show no longer lists is matched again, while a
    // settled link into another show is left alone.
    [Fact]
    public async Task ALinkTheShowNoLongerHas_IsMatchedAgain_AndOneIntoAnotherShowIsLeftAlone()
    {
        var matches = await Match(
            MatchingProvider(),
            [AnidbEpisode(1, 1, _firstAired), AnidbEpisode(2, 2, _firstAired.AddDays(7))],
            existing: [Link(1, 5, 999, MatchRating.UserVerified), Link(2, 6, 600, MatchRating.UserVerified)]
        );

        var match = Assert.Single(matches);
        Assert.Equal(1, match.AnidbEpisode.AnidbID);
        Assert.Equal("521", match.Candidate?.ID.ID);
        Assert.Equal(MatchRating.DateMatches, match.Rating);
    }

    // A group of an alternate ordering offers its own episodes, leaving out
    // one TMDB no longer lists, and a group of another show is refused.
    [Fact]
    public async Task MatchingOneGroup_OffersOnlyItsEpisodes_AndAGroupOfAnotherShowIsRefused()
    {
        var episodes = MatchingEpisodes();
        var groupEpisodes = new Mock<TMDB_AlternateOrdering_EpisodeRepository>((object)null!);
        groupEpisodes.Setup(repository => repository.GetByTmdbEpisodeGroupID("group-5")).Returns(
        [
            new TMDB_AlternateOrdering_Episode("group-5", 521) { TmdbShowID = 5, SeasonNumber = 1, EpisodeNumber = 1 },
            new TMDB_AlternateOrdering_Episode("group-5", 999) { TmdbShowID = 5, SeasonNumber = 1, EpisodeNumber = 2 },
        ]);
        groupEpisodes.Setup(repository => repository.GetByTmdbEpisodeGroupID("group-6")).Returns([]);
        var groups = new Mock<TMDB_AlternateOrdering_SeasonRepository>((object)null!);
        groups.Setup(repository => repository.GetByTmdbEpisodeGroupID(It.IsAny<string>())).Returns((string id) => id switch
        {
            "group-5" => new TMDB_AlternateOrdering_Season("group-5") { TmdbShowID = 5, SeasonNumber = 1 },
            "group-6" => new TMDB_AlternateOrdering_Season("group-6") { TmdbShowID = 6, SeasonNumber = 1 },
            _ => null,
        });
        using var scope = new RepoFactoryScope()
            .Set(MatchingShows())
            .Set(episodes)
            .Set(groupEpisodes.Object);
        var provider = MatchingProvider(episodes, alternateOrderingSeasons: groups.Object);
        var anidbEpisodes = new[] { AnidbEpisode(1, 1, _firstAired), AnidbEpisode(2, 1, _firstAired.AddDays(3), EpisodeType.Special) };

        var matches = await Match(provider, anidbEpisodes, seasonID: "group-5");
        var otherShow = await Match(provider, anidbEpisodes, seasonID: "group-6");

        Assert.Equal("521", Assert.Single(matches, match => match.AnidbEpisode.AnidbID is 1).Candidate?.ID.ID);
        Assert.All(matches, match => Assert.True(match.Candidate is null || match.Candidate.ID.ID is "521"));
        Assert.Empty(otherShow);
    }

    // An episode another anime is linked to is only kept out of the match
    // when other links are considered.
    [Fact]
    public async Task AnEpisodeAnotherAnimeClaims_IsOnlyLeftOutWhenOtherLinksAreConsidered()
    {
        var links = CachedRepo.Build<CrossRef_AniDB_Metadata_EpisodeRepository, int, CrossRef_AniDB_Metadata_Episode>(link => link.CrossRef_AniDB_Metadata_EpisodeID,
        [
            new() { CrossRef_AniDB_Metadata_EpisodeID = 1, Source = MetadataSource.TMDB, AnidbAnimeID = 31, AnidbEpisodeID = 3100, ProviderParentID = "5", ProviderID = "521" },
        ]);
        var provider = MatchingProvider(episodeLinks: new CrossRef_AniDB_TMDB_EpisodeRepository(links, null!));

        var considered = await Match(provider, [AnidbEpisode(1, 1, _firstAired)], considerOtherLinks: true);
        var ignored = await Match(provider, [AnidbEpisode(1, 1, _firstAired)], considerOtherLinks: false);

        Assert.All(considered, match => Assert.NotEqual("521", match.Candidate?.ID.ID));
        Assert.Equal("521", Assert.Single(ignored).Candidate?.ID.ID);
    }

    #endregion

    #region Upstream Failures

    [Fact]
    public void AServerErrorOrAnUnreachableTmdbIsReportedAsUnavailable()
    {
        var serverError = TmdbMetadataProvider.ToUnavailable(new GeneralHttpException(HttpStatusCode.BadGateway), TimeSpan.FromSeconds(30));
        var unreachable = TmdbMetadataProvider.ToUnavailable(new AggregateException(new HttpRequestException("No route to host.")), null);

        Assert.NotNull(serverError);
        Assert.Equal(MetadataSource.TMDB, serverError.MetadataSource);
        Assert.Equal(TimeSpan.FromSeconds(30), serverError.RetryAfter);
        Assert.IsType<GeneralHttpException>(serverError.InnerException);
        Assert.NotNull(unreachable);
        Assert.Null(unreachable.RetryAfter);
    }

    [Fact]
    public void AFailureRetryingWouldNotMendIsLeftAlone()
    {
        Assert.Null(TmdbMetadataProvider.ToUnavailable(new GeneralHttpException(HttpStatusCode.NotFound), null));
        Assert.Null(TmdbMetadataProvider.ToUnavailable(new TmdbApiKeyUnavailableException(), null));
        Assert.Null(TmdbMetadataProvider.ToUnavailable(new InvalidOperationException(), null));
    }

    [Fact]
    public void AMissingApiKeyWrappedByABlockingWaitIsTakenOutOfItsWrapper()
    {
        var missingKey = new TmdbApiKeyUnavailableException();

        Assert.Same(missingKey, TmdbMetadataProvider.ToUnavailable(new AggregateException(missingKey), null));
    }

    #endregion

    #region Pausing

    [Fact]
    public void TheCircuitBreakerIsReportedAsAPause()
    {
        var limiter = new TmdbRateLimiter(NullLogger<TmdbRateLimiter>.Instance, SettingsProvider(), TimeSpan.FromSeconds(10));
        var provider = new TmdbMetadataProvider(NullLogger<TmdbMetadataProvider>.Instance, null!, null!, limiter, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);
        var changes = 0;
        provider.PauseStatusChanged += (_, _) => changes++;

        Assert.Same(MetadataProviderPauseStatus.NotPaused, provider.PauseStatus);

        limiter.Notify5xxError();
        limiter.Notify5xxError();
        limiter.Notify5xxError();

        var status = provider.PauseStatus;
        Assert.True(status.IsPaused);
        Assert.False(string.IsNullOrWhiteSpace(status.Reason));
        Assert.NotNull(status.ResumesAt);
        Assert.True(changes > 0);
    }

    private static ConfigurationProvider<ServerSettings> SettingsProvider()
    {
        var service = new Mock<IConfigurationService>();
        service.Setup(s => s.GetConfigurationInfo<ServerSettings>()).Returns((ConfigurationInfo)null!);
        service.Setup(s => s.Load(It.IsAny<ConfigurationInfo>(), It.IsAny<bool>())).Returns(new ServerSettings());
        return new ConfigurationProvider<ServerSettings>(service.Object);
    }

    #endregion
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Matching;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Search;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers the linking service's lookups and auto-link previews, which only
/// route to the provider of the source asked about, and the registration
/// flags that tell a client whether a provider answers them at all.
/// </summary>
public class MetadataLookupPassThroughTests
{
    #region Fixtures

    private static readonly MetadataSource Source = TestSources.Plugin;

    private static readonly MetadataGuid SeriesID = new(Source, MetadataEntityType.Series, "21");

    private static readonly MetadataGuid MovieID = new(Source, MetadataEntityType.Movie, "31");

    /// <summary>
    /// A provider that looks series and films up and previews its matches.
    /// </summary>
    private sealed class LookingProvider : IMetadataSeriesLinkingProvider, IMetadataMovieLinkingProvider, IMetadataAutoLinkingProvider
    {
        public List<string> Calls { get; } = [];

        public bool WithPrequelLink { get; init; }

        public bool WithHint { get; init; }

        public IReadOnlyList<MetadataAutoLinkCandidate> Hints { get; init; } = [];

        public Exception? Failure { get; init; }

        public string Name => "Looking";

        public MetadataSource Source => MetadataLookupPassThroughTests.Source;

        public IReadOnlySet<MetadataEntityType> LinkableEntityTypes { get; } = new HashSet<MetadataEntityType> { MetadataEntityType.Series };

        public Task RefreshSeries(MetadataGuid seriesID, MetadataRefreshOptions options, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RefreshMovie(MetadataGuid movieID, MetadataRefreshOptions options, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<(IReadOnlyList<MetadataSeriesSearchResult> Page, int TotalCount)> SearchSeries(MetadataSearchOptions options, CancellationToken cancellationToken = default)
            => Task.FromResult<(IReadOnlyList<MetadataSeriesSearchResult>, int)>(([], 0));

        public Task<(IReadOnlyList<MetadataMovieSearchResult> Page, int TotalCount)> SearchMovies(MetadataSearchOptions options, CancellationToken cancellationToken = default)
            => Task.FromResult<(IReadOnlyList<MetadataMovieSearchResult>, int)>(([], 0));

        public Task<IReadOnlyList<EpisodeMatch>> MatchEpisodes(
            IAnidbAnime anime,
            IReadOnlyList<IAnidbEpisode> anidbEpisodes,
            MetadataGuid providerSeriesID,
            MetadataGuid? providerSeasonID = null,
            IReadOnlyList<IMetadataEpisodeCrossReference>? existing = null,
            bool? considerOtherLinks = null,
            CancellationToken cancellationToken = default
        )
            => Task.FromResult<IReadOnlyList<EpisodeMatch>>([]);

        public Task<MetadataSeriesSearchResult?> LookupSeries(MetadataGuid seriesID, CancellationToken cancellationToken = default)
        {
            Calls.Add($"series {seriesID}");
            return Task.FromResult<MetadataSeriesSearchResult?>(new() { ID = seriesID, Title = "Found" });
        }

        public Task<MetadataMovieSearchResult?> LookupMovie(MetadataGuid movieID, CancellationToken cancellationToken = default)
        {
            Calls.Add($"movie {movieID}");
            return Task.FromResult<MetadataMovieSearchResult?>(null);
        }

        public Task<IReadOnlyList<MetadataAutoLinkCandidate>> FindAutoLinks(int anidbAnimeID, CancellationToken cancellationToken = default)
        {
            Calls.Add($"preview {anidbAnimeID}");
            if (Failure is not null)
                throw Failure;

            return Task.FromResult<IReadOnlyList<MetadataAutoLinkCandidate>>([
                new() { Result = new MetadataSeriesSearchResult { ID = SeriesID, Title = "Match" }, AnidbAnimeID = anidbAnimeID, IsRemote = true },
                new()
                {
                    Result = new MetadataSeriesSearchResult { ID = new(Source, MetadataEntityType.Series, "22"), Title = "Loser" },
                    AnidbAnimeID = anidbAnimeID,
                    IsRemote = true,
                    Rejection = new() { Reason = MatchRejectionReason.Outranked, Details = "Second." },
                },
                new()
                {
                    Result = new MetadataSeriesSearchResult { ID = new(TestSources.AniList, MetadataEntityType.Series, "23"), Title = "Elsewhere" },
                    AnidbAnimeID = anidbAnimeID,
                    IsRemote = true,
                },
                .. WithPrequelLink
                    ? new MetadataAutoLinkCandidate[]
                    {
                        new()
                        {
                            Result = new MetadataSeriesSearchResult { ID = new(Source, MetadataEntityType.Series, "24"), Title = "Prequel's" },
                            AnidbAnimeID = anidbAnimeID,
                            IsLocal = true,
                            MatchRating = MatchRating.None,
                            Origin = MetadataAutoLinkOrigin.PrequelLink,
                            LinkMatchRating = MatchRating.UserVerified,
                            PrequelAnidbAnimeID = 6,
                        },
                    }
                    : [],
                .. WithHint
                    ? new MetadataAutoLinkCandidate[]
                    {
                        new()
                        {
                            Result = new MetadataSeriesSearchResult { ID = new(Source, MetadataEntityType.Series, "25"), Title = "Named" },
                            AnidbAnimeID = anidbAnimeID,
                            IsRemote = true,
                            MatchRating = MatchRating.DateAndTitleMatches,
                            Origin = MetadataAutoLinkOrigin.AnidbResource,
                        },
                    }
                    : [],
                .. Hints,
            ]);
        }
    }

    /// <summary>
    /// A provider that leaves every optional member to its default.
    /// </summary>
    private sealed class PlainProvider : IMetadataSeriesLinkingProvider, IMetadataAutoLinkingProvider
    {
        public string Name => "Plain";

        public MetadataSource Source => MetadataLookupPassThroughTests.Source;

        public IReadOnlySet<MetadataEntityType> LinkableEntityTypes { get; } = new HashSet<MetadataEntityType> { MetadataEntityType.Series };

        public Task RefreshSeries(MetadataGuid seriesID, MetadataRefreshOptions options, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<(IReadOnlyList<MetadataSeriesSearchResult> Page, int TotalCount)> SearchSeries(MetadataSearchOptions options, CancellationToken cancellationToken = default)
            => Task.FromResult<(IReadOnlyList<MetadataSeriesSearchResult>, int)>(([], 0));

        public Task<IReadOnlyList<EpisodeMatch>> MatchEpisodes(
            IAnidbAnime anime,
            IReadOnlyList<IAnidbEpisode> anidbEpisodes,
            MetadataGuid providerSeriesID,
            MetadataGuid? providerSeasonID = null,
            IReadOnlyList<IMetadataEpisodeCrossReference>? existing = null,
            bool? considerOtherLinks = null,
            CancellationToken cancellationToken = default
        )
            => Task.FromResult<IReadOnlyList<EpisodeMatch>>([]);

        public Task<IReadOnlyList<MetadataAutoLinkCandidate>> FindAutoLinks(int anidbAnimeID, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<MetadataAutoLinkCandidate>>([]);
    }

    private static MetadataProviderInfo Info(IMetadataProvider provider)
        => new()
        {
            ID = Guid.NewGuid(),
            Version = new(1, 0),
            Name = provider.Name,
            Description = string.Empty,
            Provider = provider,
            ConfigurationInfo = null,
            PluginInfo = null!,
            SupportsSeries = true,
            SupportsMovies = provider is IMetadataMovieProvider,
            SupportsCollections = false,
            SupportsAutoLinking = true,
            SupportsLookup = MetadataProviderManager.Overrides(provider.GetType(), typeof(IMetadataSeriesLinkingProvider), nameof(IMetadataSeriesLinkingProvider.LookupSeries)),
            Source = Source,
            AvailableEntityTypes = new HashSet<MetadataEntityType> { MetadataEntityType.Series, MetadataEntityType.Movie },
            EnabledEntityTypes = new HashSet<MetadataEntityType> { MetadataEntityType.Series, MetadataEntityType.Movie },
            IsAutoLinker = true,
        };

    private static MetadataLinkingService Build(IMetadataProvider provider, IMetadataCrossReferenceStore? links = null)
    {
        var info = Info(provider);
        var manager = new Mock<IMetadataProviderManager>();
        manager.Setup(m => m.GetAvailableProviders(It.IsAny<MetadataEntityType>(), It.IsAny<MetadataSource?>()))
            .Returns((MetadataEntityType _, MetadataSource? source) => source is null || source == Source ? [info] : []);
        manager.Setup(m => m.GetAvailableProviders(It.IsAny<bool>(), It.IsAny<bool>())).Returns([info]);
        manager.Setup(m => m.GetProviderInfo(It.IsAny<IMetadataProvider>())).Returns(info);
        // No links, so a preview lists nothing beside the search.
        var store = new Mock<IMetadataCrossReferenceStore>();
        store.Setup(s => s.GetSeriesLinks(It.IsAny<int>(), It.IsAny<MetadataSource?>())).Returns([]);
        store.Setup(s => s.GetMovieLinksForSeries(It.IsAny<int>(), It.IsAny<MetadataSource?>())).Returns([]);
        return new(NullLogger<MetadataLinkingService>.Instance, manager.Object, links ?? store.Object, Mock.Of<IMetadataService>(), null!, null!, null!, null!);
    }

    #endregion

    #region Registration

    [Fact]
    public void AProviderImplementingTheMembersIsSaidToSupportThem()
    {
        Assert.True(MetadataProviderManager.Overrides(typeof(LookingProvider), typeof(IMetadataSeriesLinkingProvider), nameof(IMetadataSeriesLinkingProvider.LookupSeries)));
        Assert.True(MetadataProviderManager.Overrides(typeof(LookingProvider), typeof(IMetadataMovieLinkingProvider), nameof(IMetadataMovieLinkingProvider.LookupMovie)));
    }

    [Fact]
    public void AProviderLeavingTheDefaultsIsNotSaidToSupportThem()
    {
        Assert.False(MetadataProviderManager.Overrides(typeof(PlainProvider), typeof(IMetadataSeriesLinkingProvider), nameof(IMetadataSeriesLinkingProvider.LookupSeries)));
        // Not implementing the interface at all is not support either.
        Assert.False(MetadataProviderManager.Overrides(typeof(PlainProvider), typeof(IMetadataMovieLinkingProvider), nameof(IMetadataMovieLinkingProvider.LookupMovie)));
    }

    #endregion

    #region Pass-throughs

    [Fact]
    public async Task ALookupGoesToTheSourcesProvider()
    {
        var provider = new LookingProvider();
        var service = Build(provider);

        var found = await service.LookupSeries(SeriesID, TestContext.Current.CancellationToken);
        var missing = await service.LookupMovie(MovieID, TestContext.Current.CancellationToken);

        Assert.Equal("Found", found?.Title);
        Assert.Null(missing);
        Assert.Equal([$"series {SeriesID}", $"movie {MovieID}"], provider.Calls);
    }

    [Fact]
    public async Task ALookupOfAnotherKindIsRefused()
    {
        var service = Build(new LookingProvider());

        await Assert.ThrowsAsync<ArgumentException>(() => service.LookupSeries(MovieID, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => service.LookupMovie(SeriesID, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ALookupAProviderDoesNotAnswerIsNotSupported()
    {
        var service = Build(new PlainProvider());

        await Assert.ThrowsAsync<NotSupportedException>(() => service.LookupSeries(SeriesID, TestContext.Current.CancellationToken));
        // Nothing links films on the source at all.
        await Assert.ThrowsAsync<NotSupportedException>(() => service.LookupMovie(MovieID, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task APreviewGoesToTheAutoLinkerAndKeepsEveryCandidate()
    {
        var provider = new LookingProvider();
        var service = Build(provider);

        var candidates = await service.PreviewAutoLink(Source, 7, TestContext.Current.CancellationToken);

        Assert.Equal(3, candidates.Count);
        var taken = candidates[0];
        Assert.Equal(SeriesID, taken.ID);
        Assert.Equal(7, taken.AnidbAnimeID);
        Assert.True(taken.IsRemote);
        Assert.Null(taken.Rejection);
        Assert.Equal(new MetadataAutoLinkRejection { Reason = MatchRejectionReason.Outranked, Details = "Second." }, candidates[1].Rejection);
        // An entry on another source is refused by the core.
        Assert.Equal(MatchRejectionReason.InvalidID, candidates[2].Rejection?.Reason);
        Assert.Equal(["preview 7"], provider.Calls);
    }

    // A preview shows what a forced search would take, the only search run
    // over a linked anime, so a hint rated above the pick wins over a link.
    [Fact]
    public async Task APreviewOfALinkedAnimeTakesAHigherHint()
    {
        var provider = new LookingProvider { WithHint = true };
        var seriesLink = new Mock<IMetadataSeriesCrossReference>();
        seriesLink.SetupGet(link => link.ProviderID).Returns(SeriesID);
        var store = new Mock<IMetadataCrossReferenceStore>();
        store.Setup(s => s.GetSeriesLinks(7, Source)).Returns([seriesLink.Object]);
        store.Setup(s => s.GetMovieLinksForSeries(7, Source)).Returns([]);
        var service = Build(provider, store.Object);

        var candidates = await service.PreviewAutoLink(Source, 7, TestContext.Current.CancellationToken);

        Assert.Equal(MatchRejectionReason.Outranked, candidates[0].Rejection?.Reason);
        var hint = Assert.Single(candidates, candidate => candidate.Origin == MetadataAutoLinkOrigin.AnidbResource);
        Assert.Null(hint.Rejection);
    }

    // The first one left is the one taken.
    [Fact]
    public async Task APreviewKeepsTheHintsOfBothOriginsInTheProvidersOrder()
    {
        MetadataAutoLinkCandidate Hint(string id, MetadataAutoLinkOrigin origin) => new()
        {
            Result = new MetadataSeriesSearchResult { ID = new(Source, MetadataEntityType.Series, id), Title = "Named" },
            AnidbAnimeID = 7,
            IsRemote = true,
            MatchRating = MatchRating.DateAndTitleMatches,
            Origin = origin,
        };
        var provider = new LookingProvider
        {
            Hints = [Hint("26", MetadataAutoLinkOrigin.CrossSourceLink), Hint("25", MetadataAutoLinkOrigin.AnidbResource)],
        };
        var service = Build(provider);

        var candidates = await service.PreviewAutoLink(Source, 7, TestContext.Current.CancellationToken);

        var hints = candidates.Where(candidate => candidate.Origin is MetadataAutoLinkOrigin.AnidbResource or MetadataAutoLinkOrigin.CrossSourceLink).ToList();
        Assert.Equal([("26", null), ("25", MatchRejectionReason.HintNotNeeded)], hints.Select(hint => (hint.ID.ID, hint.Rejection?.Reason)));
        Assert.Equal(MatchRejectionReason.Outranked, candidates[0].Rejection?.Reason);
    }

    // The anime's own links keep their stored ratings, and the same entry may be listed in each group.
    [Fact]
    public async Task APreviewListsTheAnimesLinksAndAPrequelsAfterTheSearchNeverToBeTaken()
    {
        var provider = new LookingProvider { WithPrequelLink = true };
        var seriesLink = new Mock<IMetadataSeriesCrossReference>();
        seriesLink.SetupGet(link => link.ProviderID).Returns(SeriesID);
        seriesLink.SetupGet(link => link.MatchRating).Returns(MatchRating.UserVerified);
        var movieLink = new Mock<IMetadataMovieCrossReference>();
        movieLink.SetupGet(link => link.ProviderID).Returns(MovieID);
        movieLink.SetupGet(link => link.MatchRating).Returns(MatchRating.DateAndTitleMatches);
        movieLink.SetupGet(link => link.AnidbEpisodeID).Returns(5);
        var store = new Mock<IMetadataCrossReferenceStore>();
        store.Setup(s => s.GetSeriesLinks(7, Source)).Returns([seriesLink.Object]);
        store.Setup(s => s.GetMovieLinksForSeries(7, Source)).Returns([movieLink.Object]);
        var service = Build(provider, store.Object);

        var candidates = await service.PreviewAutoLink(Source, 7, TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                (MetadataAutoLinkOrigin.Search, "21"),
                (MetadataAutoLinkOrigin.Search, "22"),
                (MetadataAutoLinkOrigin.Search, "23"),
                (MetadataAutoLinkOrigin.CurrentLink, "21"),
                (MetadataAutoLinkOrigin.CurrentLink, "31"),
                (MetadataAutoLinkOrigin.PrequelLink, "24"),
            ],
            candidates.Select(candidate => (candidate.Origin, candidate.ID.ID))
        );
        Assert.Null(candidates[0].Rejection);

        var current = candidates[3];
        Assert.Equal("Found", current.Result.Title);
        Assert.Equal((MatchRating.UserVerified, MatchRating.UserVerified), (current.MatchRating, current.LinkMatchRating));
        Assert.Equal(MatchRejectionReason.ExistingLink, current.Rejection?.Reason);

        // A film nobody can describe is still listed, by its ID.
        var film = candidates[4];
        Assert.Equal(("31", 5), (film.Result.Title, film.AnidbEpisodeID));
        Assert.Equal(MatchRejectionReason.ExistingLink, film.Rejection?.Reason);

        // The provider left the prequel's link untouched; the core turns it
        // down all the same, and never borrows the prequel's rating.
        var prequel = candidates[5];
        Assert.Equal(MatchRejectionReason.ExistingLink, prequel.Rejection?.Reason);
        Assert.Equal((MatchRating.None, MatchRating.UserVerified), (prequel.MatchRating, prequel.LinkMatchRating));
    }

    [Fact]
    public async Task APreviewOfAProviderOutOfReachFailsBeforeItReadsTheLinks()
    {
        var store = new Mock<IMetadataCrossReferenceStore>();
        var service = Build(new LookingProvider { Failure = new MetadataProviderUnavailableException(Source) }, store.Object);

        await Assert.ThrowsAnyAsync<MetadataProviderUnavailableException>(() => service.PreviewAutoLink(Source, 7, TestContext.Current.CancellationToken));

        store.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task APreviewOfASourceNobodyAutoLinksIsNotSupported()
    {
        var service = Build(new LookingProvider());

        await Assert.ThrowsAsync<NotSupportedException>(() => service.PreviewAutoLink(TestSources.AniList, 7, TestContext.Current.CancellationToken));
    }

    #endregion
}

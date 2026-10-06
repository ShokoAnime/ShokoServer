using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Events;
using Shoko.Abstractions.Metadata.Matching;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Search;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Filters;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Scheduling.Jobs.Metadata;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers <see cref="MetadataLinkingService"/> over a real store on in-memory
/// tables: what removing or replacing a link takes along, which auto-link
/// candidates and hints are taken, what an episode link records, and the
/// ratings and link events a write gives.
/// </summary>
public class MetadataLinkingServiceTests
{
    #region Fixtures

    private static readonly MetadataSource Source = TestSources.Plugin;

    private const int AnimeID = 100;

    /// <summary>
    /// The real store over in-memory tables, with the rows of each level
    /// seeded by saving them and read back in the order they were written.
    /// </summary>
    private sealed class LinkTables
    {
        private readonly WritableLinkStore _links = new();

        public LinkTables()
        {
            Series = new(_links.Series, row => row.CrossRef_AniDB_Metadata_SeriesID);
            Movies = new(_links.Movies, row => row.CrossRef_AniDB_Metadata_MovieID);
            Episodes = new(_links.Episodes, row => row.CrossRef_AniDB_Metadata_EpisodeID);
        }

        public MetadataCrossReferenceStore Store => _links.Store;

        public Rows<CrossRef_AniDB_Metadata_Series> Series { get; }

        public Rows<CrossRef_AniDB_Metadata_Movie> Movies { get; }

        public Rows<CrossRef_AniDB_Metadata_Episode> Episodes { get; }
    }

    /// <summary>
    /// The rows of one level of <see cref="LinkTables"/>.
    /// </summary>
    private sealed class Rows<TRow>(BaseCachedRepository<TRow, int> repository, Func<TRow, int> idOf) : IReadOnlyList<TRow> where TRow : class, new()
    {
        public int Count => repository.GetAll().Count;

        public TRow this[int index] => Ordered()[index];

        public void Add(TRow row) => repository.Save(row);

        public void AddRange(IEnumerable<TRow> rows)
        {
            foreach (var row in rows)
                repository.Save(row);
        }

        public IEnumerator<TRow> GetEnumerator() => Ordered().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private List<TRow> Ordered() => [.. repository.GetAll().OrderBy(idOf)];
    }

    private static CrossRef_AniDB_Metadata_Episode EpisodeLink(int anidbEpisodeID, string providerID, string providerParentID, int animeID = AnimeID)
        => new() { Source = Source, AnidbAnimeID = animeID, AnidbEpisodeID = anidbEpisodeID, ProviderID = providerID, ProviderParentID = providerParentID };

    private static CrossRef_AniDB_Metadata_Series SeriesLink(string providerID, int animeID = AnimeID)
        => new() { Source = Source, AnidbAnimeID = animeID, ProviderID = providerID };

    /// <summary>
    /// The service over a store, with mocked providers and a mocked queue
    /// that records what is enqueued.
    /// </summary>
    private sealed record Fixture(
        MetadataLinkingService Service,
        Mock<IMetadataSeriesLinkingProvider> Provider,
        Mock<IMetadataMovieLinkingProvider> MovieProvider,
        List<(Type JobType, IQueueJob Job)> Queued
    );

    private static Fixture Build(
        IMetadataCrossReferenceStore store,
        IMetadataService? metadataService = null,
        AniDB_AnimeRepository? anidbAnime = null,
        AniDB_EpisodeRepository? anidbEpisodes = null,
        AnimeSeriesRepository? animeSeries = null,
        bool providersOff = false,
        MetadataEntityType[]? enabledEntityTypes = null,
        MetadataLinkChangeTracker? linkChanges = null
    )
    {
        var provider = new Mock<IMetadataSeriesLinkingProvider>();
        provider.SetupGet(p => p.Source).Returns(Source);
        provider.SetupGet(p => p.LinkableEntityTypes).Returns(new HashSet<MetadataEntityType> { MetadataEntityType.Series, MetadataEntityType.Episode });
        var movieProvider = new Mock<IMetadataMovieLinkingProvider>();
        movieProvider.SetupGet(p => p.Source).Returns(Source);
        var enabled = enabledEntityTypes ?? [MetadataEntityType.Series, MetadataEntityType.Episode, MetadataEntityType.Movie];
        var infos = new Dictionary<IMetadataProvider, MetadataProviderInfo>
        {
            [provider.Object] = Info(provider.Object, enabled),
            [movieProvider.Object] = Info(movieProvider.Object, enabled),
        };

        var manager = new Mock<IMetadataProviderManager>();
        manager.Setup(m => m.GetAvailableProviders(It.IsAny<MetadataEntityType>(), It.IsAny<MetadataSource?>()))
            .Returns((MetadataEntityType entityType, MetadataSource? _) =>
                providersOff ? [] : [infos[entityType == MetadataEntityType.Movie ? movieProvider.Object : provider.Object]]);
        manager.Setup(m => m.GetProviderInfo(It.IsAny<IMetadataProvider>())).Returns((IMetadataProvider p) => infos[p]);
        manager.SetupGet(m => m.MetadataProviders).Returns(() => [.. infos.Values]);
        manager.Setup(m => m.GetAvailableProviders(It.IsAny<bool>(), It.IsAny<bool>())).Returns(() => providersOff ? [] : [.. infos.Values]);

        var queued = new List<(Type, IQueueJob)>();
        var queue = new Mock<IQueueScheduler>();
        queue.Setup(q => q.Enqueue(It.IsAny<Type>(), It.IsAny<Action<IQueueJob>?>(), It.IsAny<bool>()))
            .Returns((Type type, Action<IQueueJob>? configure, bool _) =>
            {
                var job = (IQueueJob)RuntimeHelpers.GetUninitializedObject(type);
                configure?.Invoke(job);
                queued.Add((type, job));
                return Task.CompletedTask;
            });
        queue.Setup(q => q.Enqueue(It.IsAny<Action<PurgeMetadataJob>?>(), It.IsAny<bool>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .Returns((Action<PurgeMetadataJob>? configure, bool _, DateTimeOffset? _, CancellationToken _) =>
            {
                var job = (PurgeMetadataJob)RuntimeHelpers.GetUninitializedObject(typeof(PurgeMetadataJob));
                configure?.Invoke(job);
                queued.Add((typeof(PurgeMetadataJob), job));
                return Task.CompletedTask;
            });
        var scheduler = new MetadataProviderScheduler(
            manager.Object,
            store,
            Mock.Of<IMetadataRefreshState>(),
            queue.Object,
            Mock.Of<IJobFactory>(),
            NullLogger<MetadataProviderScheduler>.Instance
        );

        var service = new MetadataLinkingService(
            NullLogger<MetadataLinkingService>.Instance,
            manager.Object,
            store,
            metadataService ?? new Mock<IMetadataService>().Object,
            scheduler,
            animeSeries ?? CachedRepo.Build<AnimeSeriesRepository, int, AnimeSeries>(series => series.AnimeSeriesID),
            anidbAnime ?? CachedRepo.Build<AniDB_AnimeRepository, int, AniDB_Anime>(row => row.AniDB_AnimeID),
            anidbEpisodes ?? CachedRepo.Build<AniDB_EpisodeRepository, int, AniDB_Episode>(row => row.AniDB_EpisodeID),
            linkChanges
        );
        return new(service, provider, movieProvider, queued);
    }

    private static MetadataProviderInfo Info(IMetadataProvider provider, IEnumerable<MetadataEntityType>? enabled = null)
        => new()
        {
            ID = Guid.NewGuid(),
            Version = new(1, 0),
            Name = "Test",
            Description = string.Empty,
            Provider = provider,
            ConfigurationInfo = null,
            PluginInfo = null!,
            SupportsSeries = true,
            SupportsMovies = true,
            SupportsCollections = false,
            SupportsAutoLinking = false,
            Source = Source,
            AvailableEntityTypes = new HashSet<MetadataEntityType>(),
            EnabledEntityTypes = new HashSet<MetadataEntityType>(enabled ?? []),
        };

    #endregion

    #region Bulk removal

    private static CrossRef_AniDB_Metadata_Movie MovieLink(int anidbEpisodeID, string providerID, int animeID = AnimeID)
        => new() { Source = Source, AnidbAnimeID = animeID, AnidbEpisodeID = anidbEpisodeID, ProviderID = providerID };

    private static LinkTables Library()
    {
        var store = new LinkTables();
        store.Series.AddRange([
            SeriesLink("1"),
            SeriesLink("2"),
            new() { Source = Source, AnidbAnimeID = AnimeID, ProviderID = "9", ProviderType = MetadataEntityType.Movie },
            SeriesLink("1", animeID: 200),
            new() { Source = TestSources.AniList, AnidbAnimeID = AnimeID, ProviderID = "1" },
        ]);
        store.Movies.AddRange([MovieLink(5, "8"), MovieLink(6, "8", animeID: 200)]);
        store.Episodes.AddRange([EpisodeLink(1, "11", "1"), EpisodeLink(2, "21", "2"), EpisodeLink(3, "", ""), EpisodeLink(4, "12", "1", animeID: 200)]);
        return store;
    }

    private static List<string> Purged(List<(Type JobType, IQueueJob Job)> queued)
        => [.. queued.Where(x => x.JobType == typeof(PurgeMetadataJob)).Select(x => ((PurgeMetadataJob)x.Job).EntryID)];

    [Fact]
    public async Task RemovingASourcesSeriesLinksTakesEveryEpisodeLinkButNoFilm()
    {
        var store = Library();
        var (service, _, _, queued) = Build(store.Store);

        var removed = await service.RemoveAllLinks(Source, removeMovieLinks: false, purge: true, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(7, removed);
        Assert.Equal(["9", "1"], store.Series.Select(x => x.ProviderID));
        Assert.Equal(TestSources.AniList, store.Series[1].Source);
        Assert.Equal(2, store.Movies.Count);
        Assert.Empty(store.Episodes);
        Assert.Equal(
            [ID(MetadataEntityType.Series, "1"), ID(MetadataEntityType.Series, "2")],
            Purged(queued).Order()
        );
    }

    [Fact]
    public async Task RemovingASourcesFilmLinksTakesTheirClaimsOnWholeAnimeToo()
    {
        var store = Library();
        var (service, _, _, queued) = Build(store.Store);

        var removed = await service.RemoveAllLinks(Source, removeSeriesLinks: false, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(3, removed);
        Assert.Empty(store.Movies);
        Assert.DoesNotContain(store.Series, x => x.ProviderType == MetadataEntityType.Movie);
        Assert.Equal(4, store.Episodes.Count);
        Assert.Empty(Purged(queued));
    }

    [Fact]
    public async Task RemovingAnAnimesLinksAtOneLevelLeavesTheOthers()
    {
        var token = TestContext.Current.CancellationToken;
        var seriesStore = Library();
        var (seriesService, _, _, seriesQueued) = Build(seriesStore.Store);
        var movieStore = Library();
        var (movieService, _, _, _) = Build(movieStore.Store);
        var episodeStore = Library();
        var (episodeService, _, _, _) = Build(episodeStore.Store);
        var allStore = Library();
        var (allService, _, _, _) = Build(allStore.Store);

        Assert.Equal(4, await seriesService.RemoveLinksForAnime(Source, AnimeID, MetadataEntityType.Series, purge: true, cancellationToken: token));
        Assert.Equal(2, await movieService.RemoveLinksForAnime(Source, AnimeID, MetadataEntityType.Movie, cancellationToken: token));
        Assert.Equal(3, await episodeService.RemoveLinksForAnime(Source, AnimeID, MetadataEntityType.Episode, cancellationToken: token));
        Assert.Equal(7, await allService.RemoveLinksForAnime(Source, AnimeID, cancellationToken: token));

        // A film's claim on the anime is left in place, so the episode link
        // naming no series is too.
        Assert.Equal(["9", "1", "1"], seriesStore.Series.Select(x => x.ProviderID));
        Assert.Equal([3, 4], seriesStore.Episodes.Select(x => x.AnidbEpisodeID));
        Assert.Equal([ID(MetadataEntityType.Series, "1"), ID(MetadataEntityType.Series, "2")], Purged(seriesQueued).Order());
        Assert.Equal([6], movieStore.Movies.Select(x => x.AnidbEpisodeID));
        Assert.Equal(4, movieStore.Series.Count);
        Assert.Equal([4], episodeStore.Episodes.Select(x => x.AnidbEpisodeID));
        Assert.Equal(5, episodeStore.Series.Count);
        Assert.All(allStore.Series.Concat<CrossRef_AniDB_Metadata>(allStore.Movies).Concat(allStore.Episodes),
            x => Assert.True(x.AnidbAnimeID != AnimeID || x.Source != Source));
    }

    [Fact]
    public async Task RemovingAnEpisodesLinksTakesItsFilmsOrItsEpisodes()
    {
        var store = Library();
        store.Episodes.Add(EpisodeLink(5, "51", "2"));
        var (service, _, _, queued) = Build(store.Store);

        Assert.Equal(1, await service.RemoveLinksForEpisode(Source, 5, MetadataEntityType.Movie, purge: true, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(1, await service.RemoveLinksForEpisode(Source, 5, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal([6], store.Movies.Select(x => x.AnidbEpisodeID));
        Assert.DoesNotContain(store.Episodes, x => x.AnidbEpisodeID == 5);
        Assert.Equal([ID(MetadataEntityType.Movie, "8")], Purged(queued));
    }

    [Fact]
    public async Task RemovingTheLinksToASeriesTakesThemFromEveryAnime()
    {
        var store = Library();
        var (service, _, _, queued) = Build(store.Store);
        var series = new MetadataGuid(Source, MetadataEntityType.Series, "1");

        Assert.Equal(4, await service.RemoveLinksTo(series, purge: true, TestContext.Current.CancellationToken));
        Assert.Equal(2, await service.RemoveLinksTo(new(Source, MetadataEntityType.Movie, "8"), cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(["2", "9", "1"], store.Series.Select(x => x.ProviderID));
        Assert.Equal(TestSources.AniList, store.Series[2].Source);
        Assert.Equal([2, 3], store.Episodes.Select(x => x.AnidbEpisodeID));
        Assert.Empty(store.Movies);
        Assert.Equal([series.ToString()], Purged(queued));
    }

    [Fact]
    public async Task RemovingTheLinksToASeriesTakesTheEpisodeLinksToNothingOfAnimeLeftWithoutOne()
    {
        var store = new LinkTables();
        store.Series.AddRange([SeriesLink("1"), SeriesLink("1", animeID: 200), SeriesLink("2", animeID: 200)]);
        store.Episodes.AddRange([EpisodeLink(1, "11", "1"), EpisodeLink(3, "", ""), EpisodeLink(4, "", "", animeID: 200)]);
        var (service, _, _, _) = Build(store.Store);

        Assert.Equal(4, await service.RemoveLinksTo(new(Source, MetadataEntityType.Series, "1"), cancellationToken: TestContext.Current.CancellationToken));

        // Anime 200 still has a series, so its episode link to nothing stays.
        Assert.Equal([4], store.Episodes.Select(x => x.AnidbEpisodeID));
        Assert.Equal(["2"], store.Series.Select(x => x.ProviderID));
    }

    private static string ID(MetadataEntityType entityType, string id)
        => new MetadataGuid(Source, entityType, id).ToString();

    #endregion

    #region Removing a series link

    [Fact]
    public async Task RemovingASeriesLinkTakesOnlyItsOwnEpisodeLinks()
    {
        var store = new LinkTables();
        store.Series.AddRange([SeriesLink("1"), SeriesLink("2")]);
        store.Episodes.AddRange([EpisodeLink(1, "11", "1"), EpisodeLink(2, "21", "2"), EpisodeLink(3, "", ""), EpisodeLink(4, "12", "1", animeID: 200)]);
        var (service, _, _, queued) = Build(store.Store);

        var removed = await service.RemoveSeriesLink(SeriesRequest("1"), TestContext.Current.CancellationToken);

        Assert.True(removed);
        Assert.Empty(queued);
        Assert.Equal(["2"], store.Series.Where(x => x.AnidbAnimeID == AnimeID).Select(x => x.ProviderID));
        // Another linked show's episodes, the unparented ones while a show is
        // left, and another anime's links all stay.
        Assert.Equal([2, 3, 4], store.Episodes.Select(x => x.AnidbEpisodeID).Order());
    }

    [Fact]
    public async Task RemovingTheLastSeriesLinkAlsoTakesUnparentedEpisodeLinks()
    {
        var store = new LinkTables();
        store.Series.Add(SeriesLink("1"));
        store.Episodes.AddRange([EpisodeLink(1, "11", "1"), EpisodeLink(3, "", "")]);
        var (service, _, _, _) = Build(store.Store);

        await service.RemoveSeriesLink(SeriesRequest("1"), TestContext.Current.CancellationToken);

        Assert.Empty(store.Episodes);
    }

    [Fact]
    public async Task ALinkIsRemovedWhileItsProviderIsOffButNotAdded()
    {
        var store = new LinkTables();
        store.Series.AddRange([SeriesLink("1"), new() { Source = Source, AnidbAnimeID = AnimeID, ProviderID = "9", ProviderType = MetadataEntityType.Movie }]);
        store.Movies.Add(MovieLink(5, "8"));
        store.Episodes.Add(EpisodeLink(1, "11", "1"));
        var (service, _, _, _) = Build(store.Store, anidbEpisodes: ResetEpisodes(), providersOff: true);
        var token = TestContext.Current.CancellationToken;

        Assert.True(await service.RemoveSeriesLink(SeriesRequest("1"), token));
        Assert.True(await service.RemoveSeriesLink(SeriesRequest("9", MetadataEntityType.Movie), token));
        Assert.True(await service.RemoveMovieLink(new()
        {
            Source = Source,
            EntityType = MetadataEntityType.Movie,
            AnidbAnimeID = AnimeID,
            AnidbEpisodeID = 5,
            ProviderID = new(Source, MetadataEntityType.Movie, "8"),
        }, token));
        Assert.True(await service.ResetEpisodeLinks(Source, AnimeID, allowAutoMatch: true, token));

        Assert.Empty(store.Series);
        Assert.Empty(store.Movies);
        Assert.Empty(store.Episodes);
        await Assert.ThrowsAsync<NotSupportedException>(() => service.AddSeriesLink(SeriesRequest("1"), token));
    }

    [Fact]
    public async Task RemovingASeriesLinkLeavesFilmLinksAlone()
    {
        var store = new LinkTables();
        store.Series.Add(SeriesLink("1"));
        store.Movies.Add(new() { Source = Source, AnidbAnimeID = AnimeID, AnidbEpisodeID = 5, ProviderID = "1" });
        var (service, _, _, _) = Build(store.Store);

        await service.RemoveSeriesLink(SeriesRequest("1"), TestContext.Current.CancellationToken);

        Assert.Single(store.Movies);
    }

    [Fact]
    public async Task RemovingAWholeFilmTakesItsFilmLinks()
    {
        var store = new LinkTables();
        store.Series.Add(SeriesLink("1"));
        store.Movies.AddRange([
            new() { Source = Source, AnidbAnimeID = AnimeID, AnidbEpisodeID = 5, ProviderID = "9" },
            new() { Source = Source, AnidbAnimeID = AnimeID, AnidbEpisodeID = 6, ProviderID = "8" },
        ]);
        var (service, _, _, queued) = Build(store.Store);

        var removed = await service.RemoveSeriesLink(SeriesRequest("9", MetadataEntityType.Movie) with { Purge = true }, TestContext.Current.CancellationToken);

        Assert.True(removed);
        Assert.Equal(["8"], store.Movies.Select(x => x.ProviderID));
        Assert.Single(store.Series);
        var purge = Assert.IsType<PurgeMetadataJob>(Assert.Single(queued).Job);
        Assert.Equal(new MetadataGuid(Source, MetadataEntityType.Movie, "9").ToString(), purge.EntryID);
    }

    [Fact]
    public async Task RemovingAWholeFilmLeavesASeriesWithTheSameID()
    {
        // Series and film IDs are separate spaces on a source.
        var store = new LinkTables();
        store.Series.AddRange([
            SeriesLink("9"),
            new() { Source = Source, AnidbAnimeID = AnimeID, ProviderID = "9", ProviderType = MetadataEntityType.Movie },
            SeriesLink("1"),
        ]);
        var (service, _, _, _) = Build(store.Store);

        var removed = await service.RemoveSeriesLink(SeriesRequest("9", MetadataEntityType.Movie), TestContext.Current.CancellationToken);

        Assert.True(removed);
        Assert.Equal(["9", "1"], store.Series.Select(x => x.ProviderID));
        Assert.All(store.Series, x => Assert.Equal(MetadataEntityType.Series, x.ProviderType));
    }

    [Fact]
    public async Task RemovingAnUnlinkedSeriesChangesNothing()
    {
        var store = new LinkTables();
        store.Series.Add(SeriesLink("2"));
        store.Episodes.Add(EpisodeLink(1, "11", "1"));
        var (service, _, _, _) = Build(store.Store);

        var removed = await service.RemoveSeriesLink(SeriesRequest("1"), TestContext.Current.CancellationToken);

        Assert.False(removed);
        Assert.Single(store.Episodes);
    }

    #endregion

    #region Replacing a series link

    [Fact]
    public async Task ReplacingSeriesLinksTakesTheReplacedEpisodeLinks()
    {
        var store = new LinkTables();
        store.Series.AddRange([SeriesLink("1"), SeriesLink("2")]);
        store.Episodes.AddRange([EpisodeLink(1, "11", "1"), EpisodeLink(2, "21", "2"), EpisodeLink(3, "31", "3"), EpisodeLink(4, "", "")]);
        var (service, _, _, _) = Build(store.Store);

        await service.AddSeriesLink(SeriesRequest("3") with { Additive = false }, TestContext.Current.CancellationToken);

        Assert.Equal(["3"], store.Series.Select(x => x.ProviderID));
        Assert.Equal([3, 4], store.Episodes.Select(x => x.AnidbEpisodeID).Order());
    }

    [Fact]
    public async Task AFilmClaimingTheWholeAnimeIsKeptAsTheFilm()
    {
        var store = new LinkTables();
        var (service, _, _, _) = Build(store.Store);

        await service.AddSeriesLink(SeriesRequest("9", MetadataEntityType.Movie), TestContext.Current.CancellationToken);

        var link = Assert.Single(store.Series);
        Assert.Equal(new MetadataGuid(Source, MetadataEntityType.Movie, "9"), ((IMetadataCrossReference)link).ProviderID);
        Assert.Equal(MetadataEntityType.Series, link.EntityType);
    }

    [Fact]
    public async Task AddingASeriesLinkKeepsOtherEpisodeLinks()
    {
        var store = new LinkTables();
        store.Series.Add(SeriesLink("1"));
        store.Episodes.Add(EpisodeLink(1, "11", "1"));
        var (service, _, _, _) = Build(store.Store);

        await service.AddSeriesLink(SeriesRequest("2"), TestContext.Current.CancellationToken);

        Assert.Equal(2, store.Series.Count);
        Assert.Single(store.Episodes);
    }

    #endregion

    #region Matching a linked series

    private static void SetupMatch(Mock<IMetadataSeriesLinkingProvider> provider, IReadOnlyList<EpisodeMatch> matches, List<MetadataGuid> asked)
        => provider
            .Setup(p => p.MatchEpisodes(
                It.IsAny<IAnidbAnime>(),
                It.IsAny<IReadOnlyList<IAnidbEpisode>>(),
                It.IsAny<MetadataGuid>(),
                It.IsAny<MetadataGuid?>(),
                It.IsAny<IReadOnlyList<IMetadataEpisodeCrossReference>?>(),
                It.IsAny<bool?>(),
                It.IsAny<CancellationToken>()
            ))
            .Callback((IAnidbAnime _, IReadOnlyList<IAnidbEpisode> _, MetadataGuid seriesID, MetadataGuid? _, IReadOnlyList<IMetadataEpisodeCrossReference>? _,
                bool? _, CancellationToken _) => asked.Add(seriesID))
            .ReturnsAsync(matches);

    private static (AniDB_AnimeRepository Anime, AniDB_EpisodeRepository Episodes, AniDB_Episode Episode) OneEpisodeAnime()
    {
        var anidbEpisode = new AniDB_Episode { AniDB_EpisodeID = 1, EpisodeID = 11, AnimeID = AnimeID, EpisodeType = EpisodeType.Episode };
        return (
            CachedRepo.Build<AniDB_AnimeRepository, int, AniDB_Anime>(row => row.AniDB_AnimeID, new AniDB_Anime { AniDB_AnimeID = 1, AnimeID = AnimeID }),
            CachedRepo.Build<AniDB_EpisodeRepository, int, AniDB_Episode>(row => row.AniDB_EpisodeID, anidbEpisode),
            anidbEpisode
        );
    }

    private static EpisodeMatch Match(AniDB_Episode anidbEpisode, string seriesID, string episodeID)
    {
        var candidate = new Mock<IEpisode>();
        candidate.SetupGet(e => e.ID).Returns(new MetadataGuid(Source, MetadataEntityType.Episode, episodeID));
        candidate.SetupGet(e => e.SeriesID).Returns(new MetadataGuid(Source, MetadataEntityType.Series, seriesID));
        return new() { AnidbEpisode = anidbEpisode, Candidate = candidate.Object, Rating = MatchRating.DateAndTitleMatches };
    }

    [Fact]
    public async Task LinkingASeriesMatchesTheAnimesEpisodesToIt()
    {
        var store = new LinkTables();
        var (anime, episodes, anidbEpisode) = OneEpisodeAnime();
        var (service, provider, _, queued) = Build(store.Store, anidbAnime: anime, anidbEpisodes: episodes);
        var asked = new List<MetadataGuid>();
        SetupMatch(provider, [Match(anidbEpisode, "2", "21")], asked);

        await service.AddSeriesLink(SeriesRequest("2"), TestContext.Current.CancellationToken);

        Assert.Equal([new MetadataGuid(Source, MetadataEntityType.Series, "2")], asked);
        var link = Assert.Single(store.Episodes);
        Assert.Equal("21", link.ProviderID);
        Assert.Equal("2", link.ProviderParentID);
        Assert.Empty(queued);
    }

    [Fact]
    public async Task LinkingASeriesMatchesNothingWhereEpisodesAreNotLinked()
    {
        var store = new LinkTables();
        var (anime, episodes, anidbEpisode) = OneEpisodeAnime();
        var (service, provider, _, _) = Build(store.Store, anidbAnime: anime, anidbEpisodes: episodes);
        provider.SetupGet(p => p.LinkableEntityTypes).Returns(new HashSet<MetadataEntityType> { MetadataEntityType.Series });
        var asked = new List<MetadataGuid>();
        SetupMatch(provider, [Match(anidbEpisode, "2", "21")], asked);

        await service.AddSeriesLink(SeriesRequest("2"), TestContext.Current.CancellationToken);
        await service.AddSeriesLink(SeriesRequest("9", MetadataEntityType.Movie), TestContext.Current.CancellationToken);

        Assert.Empty(asked);
        Assert.Empty(store.Episodes);
    }

    [Fact]
    public async Task AFilmClaimingTheWholeAnimeHasNoEpisodesMatched()
    {
        var store = new LinkTables();
        var (anime, episodes, anidbEpisode) = OneEpisodeAnime();
        var (service, provider, _, _) = Build(store.Store, anidbAnime: anime, anidbEpisodes: episodes);
        var asked = new List<MetadataGuid>();
        SetupMatch(provider, [Match(anidbEpisode, "9", "91")], asked);

        await service.AddSeriesLink(SeriesRequest("9", MetadataEntityType.Movie), TestContext.Current.CancellationToken);

        Assert.Empty(asked);
        Assert.Empty(store.Episodes);
    }

    #endregion

    #region Auto-linking

    private static MetadataAutoLinkCandidate Candidate(
        string id,
        MetadataEntityType? entityType = null,
        int? anidbEpisodeID = null,
        int animeID = AnimeID,
        MetadataSource? source = null,
        MatchRejectionReason? rejected = null
    )
    {
        var entryID = new MetadataGuid(source ?? Source, entityType ?? MetadataEntityType.Series, id);
        return new()
        {
            Result = entryID.EntityType == MetadataEntityType.Movie
                ? new MetadataMovieSearchResult { ID = entryID, Title = $"Film {id}" }
                : new MetadataSeriesSearchResult { ID = entryID, Title = $"Series {id}" },
            AnidbAnimeID = animeID,
            AnidbEpisodeID = anidbEpisodeID,
            MatchRating = MatchRating.TitleMatches,
            IsRemote = true,
            Rejection = rejected is { } reason ? new() { Reason = reason, Details = "By the provider." } : null,
        };
    }

    [Fact]
    public void TheCoreRefusesWhatCannotBeLinkedAndKeepsTheProvidersReasons()
    {
        var episodes = CachedRepo.Build<AniDB_EpisodeRepository, int, AniDB_Episode>(
            row => row.AniDB_EpisodeID,
            new AniDB_Episode { AniDB_EpisodeID = 1, EpisodeID = 5, AnimeID = AnimeID, EpisodeType = EpisodeType.Episode },
            new AniDB_Episode { AniDB_EpisodeID = 2, EpisodeID = 6, AnimeID = 200, EpisodeType = EpisodeType.Episode }
        );
        var (service, _, _, _) = Build(new LinkTables().Store, anidbEpisodes: episodes);

        var reviewed = service.ReviewAutoLinks(Source, AnimeID, [
            Candidate("1"),
            Candidate("2", rejected: MatchRejectionReason.TitleMismatch),
            Candidate("3", source: TestSources.AniList),
            Candidate("4", MetadataEntityType.Episode),
            Candidate("5", animeID: 200),
            Candidate("6", MetadataEntityType.Movie, anidbEpisodeID: 6),
            Candidate("7", anidbEpisodeID: 5),
            Candidate("8", MetadataEntityType.Movie, anidbEpisodeID: 5),
        ]);

        Assert.Equal(
            [
                null,
                MatchRejectionReason.TitleMismatch,
                MatchRejectionReason.InvalidID,
                MatchRejectionReason.InvalidID,
                MatchRejectionReason.InvalidID,
                MatchRejectionReason.InvalidID,
                MatchRejectionReason.InvalidID,
                null,
            ],
            reviewed.Select(candidate => candidate.Rejection?.Reason)
        );
        Assert.Equal("By the provider.", reviewed[1].Rejection?.Details);
        Assert.All(reviewed.Skip(2).SkipLast(1), candidate => Assert.False(string.IsNullOrEmpty(candidate.Rejection?.Details)));
    }

    [Fact]
    public void AKindTurnedOffForTheSourceIsRefused()
    {
        var episodes = CachedRepo.Build<AniDB_EpisodeRepository, int, AniDB_Episode>(
            row => row.AniDB_EpisodeID,
            new AniDB_Episode { AniDB_EpisodeID = 1, EpisodeID = 5, AnimeID = AnimeID, EpisodeType = EpisodeType.Episode }
        );
        var (service, _, _, _) = Build(new LinkTables().Store, anidbEpisodes: episodes, enabledEntityTypes: [MetadataEntityType.Series]);

        var reviewed = service.ReviewAutoLinks(Source, AnimeID, [Candidate("1"), Candidate("8", MetadataEntityType.Movie, anidbEpisodeID: 5)]);

        Assert.Null(reviewed[0].Rejection);
        Assert.Equal(MatchRejectionReason.KindDisabled, reviewed[1].Rejection?.Reason);
    }

    [Fact]
    public async Task WhatIsTakenIsLinkedAndTheRestIsNot()
    {
        var store = new LinkTables();
        var episodes = CachedRepo.Build<AniDB_EpisodeRepository, int, AniDB_Episode>(
            row => row.AniDB_EpisodeID,
            new AniDB_Episode { AniDB_EpisodeID = 1, EpisodeID = 5, AnimeID = AnimeID, EpisodeType = EpisodeType.Episode }
        );
        var (service, _, _, queued) = Build(store.Store, anidbEpisodes: episodes);

        var linked = await service.ApplyAutoLinks(
            Source,
            AnimeID,
            [Candidate("1"), Candidate("8", MetadataEntityType.Movie, anidbEpisodeID: 5), Candidate("2", rejected: MatchRejectionReason.Outranked)],
            replace: false,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(["1", "8"], linked.Select(candidate => candidate.ID.ID));
        Assert.Equal(["1"], store.Series.Select(x => x.ProviderID));
        var movie = Assert.Single(store.Movies);
        Assert.Equal(("8", 5), (movie.ProviderID, movie.AnidbEpisodeID));
        Assert.Empty(queued);
    }

    // Even one the provider left without a reason.
    [Theory]
    [InlineData(MetadataAutoLinkOrigin.CurrentLink)]
    [InlineData(MetadataAutoLinkOrigin.PrequelLink)]
    public async Task ALinkListedForContextIsNeverTaken(MetadataAutoLinkOrigin origin)
    {
        var store = new LinkTables();
        var (service, _, _, _) = Build(store.Store);

        var linked = await service.ApplyAutoLinks(Source, AnimeID, [Candidate("1") with { Origin = origin }], replace: true, TestContext.Current.CancellationToken);

        Assert.Empty(linked);
        Assert.Empty(store.Series);
        Assert.Equal(MatchRejectionReason.ExistingLink, Assert.Single(service.ReviewAutoLinks(Source, AnimeID, [Candidate("1") with { Origin = origin }])).Rejection?.Reason);
    }

    // A hint is an entry the anime's AniDB resources name; it is never written as verified by a user.
    [Fact]
    public async Task AHintIsTakenWhenTheSearchTookNothing()
    {
        var store = new LinkTables();
        var (service, _, _, _) = Build(store.Store);

        var linked = await service.ApplyAutoLinks(
            Source,
            AnimeID,
            [
                Candidate("1", rejected: MatchRejectionReason.TitleMismatch),
                Candidate("2") with { Origin = MetadataAutoLinkOrigin.AnidbResource, MatchRating = MatchRating.UserVerified },
            ],
            replace: false,
            TestContext.Current.CancellationToken
        );

        var taken = Assert.Single(linked);
        Assert.Equal(("2", MatchRating.FirstAvailable), (taken.ID.ID, taken.MatchRating));
        Assert.Equal(["2"], store.Series.Select(x => x.ProviderID));
    }

    // Rated no higher than the pick, whether it names the same entry or another.
    [Fact]
    public async Task AHintIsNotTakenBesideWhatTheSearchTook()
    {
        var store = new LinkTables();
        var (service, _, _, _) = Build(store.Store);
        IReadOnlyList<MetadataAutoLinkCandidate> candidates =
        [
            Candidate("1"),
            Candidate("1") with { Origin = MetadataAutoLinkOrigin.AnidbResource },
            Candidate("2") with { Origin = MetadataAutoLinkOrigin.AnidbResource },
        ];

        var linked = await service.ApplyAutoLinks(Source, AnimeID, candidates, replace: false, TestContext.Current.CancellationToken);
        var reviewed = service.ReviewAutoLinks(Source, AnimeID, candidates);

        Assert.Equal(["1"], linked.Select(candidate => candidate.ID.ID));
        Assert.Equal(["1"], store.Series.Select(x => x.ProviderID));
        Assert.Equal([null, MatchRejectionReason.HintNotNeeded, MatchRejectionReason.HintNotNeeded], reviewed.Select(candidate => candidate.Rejection?.Reason));
    }

    // Strictly higher, for the same place; the pick is turned down.
    [Fact]
    public async Task AHintRatedHigherIsTakenInsteadOfTheSearchsPick()
    {
        var store = new LinkTables();
        var (service, _, _, _) = Build(store.Store);
        IReadOnlyList<MetadataAutoLinkCandidate> candidates =
        [
            Candidate("1"),
            Candidate("2") with { Origin = MetadataAutoLinkOrigin.AnidbResource, MatchRating = MatchRating.DateAndTitleMatches },
            Candidate("3") with { Origin = MetadataAutoLinkOrigin.AnidbResource, MatchRating = MatchRating.DateAndTitleMatches },
        ];

        var reviewed = service.ReviewAutoLinks(Source, AnimeID, candidates);
        var linked = await service.ApplyAutoLinks(Source, AnimeID, candidates, replace: false, TestContext.Current.CancellationToken);

        Assert.Equal([MatchRejectionReason.Outranked, MatchRejectionReason.None, MatchRejectionReason.HintNotNeeded],
            reviewed.Select(candidate => candidate.Rejection?.Reason ?? MatchRejectionReason.None));
        Assert.Equal([("2", MatchRating.DateAndTitleMatches)], linked.Select(candidate => (candidate.ID.ID, candidate.MatchRating)));
        Assert.Equal(["2"], store.Series.Select(x => x.ProviderID));
    }

    // A film hint competes with the picks for its own episode, never with
    // another episode's, and is not added beside those.
    [Fact]
    public void AHintOnlyReplacesThePickForItsOwnPlace()
    {
        var episodes = CachedRepo.Build<AniDB_EpisodeRepository, int, AniDB_Episode>(
            row => row.AniDB_EpisodeID,
            new AniDB_Episode { AniDB_EpisodeID = 1, EpisodeID = 5, AnimeID = AnimeID, EpisodeType = EpisodeType.Episode },
            new AniDB_Episode { AniDB_EpisodeID = 2, EpisodeID = 6, AnimeID = AnimeID, EpisodeType = EpisodeType.Episode }
        );
        var (service, _, _, _) = Build(new LinkTables().Store, anidbEpisodes: episodes);

        var elsewhere = service.ReviewAutoLinks(Source, AnimeID,
        [
            Candidate("8", MetadataEntityType.Movie, anidbEpisodeID: 5),
            Candidate("9", MetadataEntityType.Movie, anidbEpisodeID: 6) with
            {
                Origin = MetadataAutoLinkOrigin.AnidbResource,
                MatchRating = MatchRating.DateAndTitleMatches,
            },
        ]);
        var samePlace = service.ReviewAutoLinks(Source, AnimeID,
        [
            Candidate("7", MetadataEntityType.Movie, anidbEpisodeID: 6),
            Candidate("8", MetadataEntityType.Movie, anidbEpisodeID: 5),
            Candidate("9", MetadataEntityType.Movie, anidbEpisodeID: 5) with
            {
                Origin = MetadataAutoLinkOrigin.AnidbResource,
                MatchRating = MatchRating.DateAndTitleMatches,
            },
        ]);

        Assert.Null(elsewhere[0].Rejection);
        Assert.Equal(MatchRejectionReason.HintNotNeeded, elsewhere[1].Rejection?.Reason);
        Assert.Equal([MatchRejectionReason.None, MatchRejectionReason.Outranked, MatchRejectionReason.None],
            samePlace.Select(candidate => candidate.Rejection?.Reason ?? MatchRejectionReason.None));
    }

    // Taken over every film the search placed on the anime's episodes when rated above the best,
    // left when one of them is rated as high.
    [Fact]
    public async Task AHintForTheWholeAnimeCompetesWithEveryPick()
    {
        var episodes = CachedRepo.Build<AniDB_EpisodeRepository, int, AniDB_Episode>(
            row => row.AniDB_EpisodeID,
            new AniDB_Episode { AniDB_EpisodeID = 1, EpisodeID = 5, AnimeID = AnimeID, EpisodeType = EpisodeType.Episode },
            new AniDB_Episode { AniDB_EpisodeID = 2, EpisodeID = 6, AnimeID = AnimeID, EpisodeType = EpisodeType.Episode }
        );
        var store = new LinkTables();
        var (service, _, _, _) = Build(store.Store, anidbEpisodes: episodes);
        IReadOnlyList<MetadataAutoLinkCandidate> outranked =
        [
            Candidate("7", MetadataEntityType.Movie, anidbEpisodeID: 5) with { MatchRating = MatchRating.TitleKindaMatches },
            Candidate("8", MetadataEntityType.Movie, anidbEpisodeID: 6),
            Candidate("1") with { Origin = MetadataAutoLinkOrigin.AnidbResource, MatchRating = MatchRating.DateAndTitleMatches },
        ];

        var reviewed = service.ReviewAutoLinks(Source, AnimeID, outranked);
        var tied = service.ReviewAutoLinks(Source, AnimeID,
        [
            Candidate("7", MetadataEntityType.Movie, anidbEpisodeID: 5) with { MatchRating = MatchRating.DateAndTitleMatches },
            Candidate("8", MetadataEntityType.Movie, anidbEpisodeID: 6),
            Candidate("1") with { Origin = MetadataAutoLinkOrigin.AnidbResource, MatchRating = MatchRating.DateAndTitleMatches },
        ]);
        var linked = await service.ApplyAutoLinks(Source, AnimeID, outranked, replace: false, TestContext.Current.CancellationToken);

        Assert.Equal([MatchRejectionReason.Outranked, MatchRejectionReason.Outranked, MatchRejectionReason.None],
            reviewed.Select(candidate => candidate.Rejection?.Reason ?? MatchRejectionReason.None));
        Assert.Equal([MatchRejectionReason.None, MatchRejectionReason.None, MatchRejectionReason.HintNotNeeded],
            tied.Select(candidate => candidate.Rejection?.Reason ?? MatchRejectionReason.None));
        Assert.Equal(["1"], linked.Select(candidate => candidate.ID.ID));
        Assert.Equal(["1"], store.Series.Select(x => x.ProviderID));
        Assert.Empty(store.Movies);
    }

    // It replaces that pick when rated above it.
    [Fact]
    public async Task AFilmHintCompetesWithAPickForTheWholeAnime()
    {
        var episodes = CachedRepo.Build<AniDB_EpisodeRepository, int, AniDB_Episode>(
            row => row.AniDB_EpisodeID,
            new AniDB_Episode { AniDB_EpisodeID = 1, EpisodeID = 5, AnimeID = AnimeID, EpisodeType = EpisodeType.Episode }
        );
        var store = new LinkTables();
        var (service, _, _, _) = Build(store.Store, anidbEpisodes: episodes);
        IReadOnlyList<MetadataAutoLinkCandidate> candidates =
        [
            Candidate("1"),
            Candidate("8", MetadataEntityType.Movie, anidbEpisodeID: 5) with
            {
                Origin = MetadataAutoLinkOrigin.AnidbResource,
                MatchRating = MatchRating.DateAndTitleMatches,
            },
        ];

        var reviewed = service.ReviewAutoLinks(Source, AnimeID, candidates);
        var linked = await service.ApplyAutoLinks(Source, AnimeID, candidates, replace: false, TestContext.Current.CancellationToken);

        Assert.Equal(MatchRejectionReason.Outranked, reviewed[0].Rejection?.Reason);
        Assert.Null(reviewed[1].Rejection);
        Assert.Equal(["8"], linked.Select(candidate => candidate.ID.ID));
        Assert.Empty(store.Series);
        var movie = Assert.Single(store.Movies);
        Assert.Equal(("8", 5), (movie.ProviderID, movie.AnidbEpisodeID));
    }

    // From the AniDB resources or another source's links alike, on a search that does not replace
    // the anime's links.
    [Theory]
    [InlineData(MetadataAutoLinkOrigin.AnidbResource)]
    [InlineData(MetadataAutoLinkOrigin.CrossSourceLink)]
    public void AHintRatedHigherStillNeverGoesOverALink(MetadataAutoLinkOrigin origin)
    {
        var store = new LinkTables();
        store.Series.Add(SeriesLink("4"));
        var (service, _, _, _) = Build(store.Store);

        var reviewed = service.ReviewAutoLinks(Source, AnimeID,
        [
            Candidate("1"),
            Candidate("2") with { Origin = origin, MatchRating = MatchRating.DateAndTitleMatches },
        ]);

        Assert.Null(reviewed[0].Rejection);
        Assert.Equal(MatchRejectionReason.HintNotNeeded, reviewed[1].Rejection?.Reason);
    }

    // A search result the core refuses leaves the search with nothing taken,
    // so the hint may be.
    [Fact]
    public void AHintIsTakenWhenTheCoreRefusedWhatTheSearchTook()
    {
        var (service, _, _, _) = Build(new LinkTables().Store);

        var reviewed = service.ReviewAutoLinks(Source, AnimeID,
        [
            Candidate("0", MetadataEntityType.Episode),
            Candidate("2") with { Origin = MetadataAutoLinkOrigin.AnidbResource },
        ]);

        Assert.NotNull(reviewed[0].Rejection);
        Assert.Null(reviewed[1].Rejection);
    }

    // A replacing search reviews hints as for an anime with no link, so a hint beats the anime's
    // links when the search took nothing else.
    [Fact]
    public async Task AReplacingSearchTakesAHintOverTheAnimesLinks()
    {
        var store = new LinkTables();
        var verified = SeriesLink("1");
        verified.MatchRating = MatchRating.UserVerified;
        store.Series.Add(verified);
        store.Episodes.Add(EpisodeLink(1, "11", "1"));
        var (service, _, _, _) = Build(store.Store);
        IReadOnlyList<MetadataAutoLinkCandidate> candidates =
        [
            Candidate("3", rejected: MatchRejectionReason.TitleMismatch),
            Candidate("2") with { Origin = MetadataAutoLinkOrigin.AnidbResource },
        ];

        var kept = service.ReviewAutoLinks(Source, AnimeID, candidates);
        var linked = await service.ApplyAutoLinks(Source, AnimeID, candidates, replace: true, TestContext.Current.CancellationToken);

        Assert.Equal(MatchRejectionReason.HintNotNeeded, kept[1].Rejection?.Reason);
        Assert.Equal(["2"], linked.Select(candidate => candidate.ID.ID));
        Assert.Equal(["2"], store.Series.Select(x => x.ProviderID));
        Assert.Empty(store.Episodes);
    }

    // The pick is turned down even though the anime is already linked to it.
    [Fact]
    public async Task AReplacingSearchTakesAHigherHintOverThePickItIsLinkedTo()
    {
        var store = new LinkTables();
        store.Series.Add(SeriesLink("1"));
        var (service, _, _, _) = Build(store.Store);
        IReadOnlyList<MetadataAutoLinkCandidate> candidates =
        [
            Candidate("1"),
            Candidate("2") with { Origin = MetadataAutoLinkOrigin.AnidbResource, MatchRating = MatchRating.DateAndTitleMatches },
        ];

        var reviewed = service.ReviewAutoLinks(Source, AnimeID, candidates, replace: true);
        var linked = await service.ApplyAutoLinks(Source, AnimeID, candidates, replace: true, TestContext.Current.CancellationToken);

        Assert.Equal([MatchRejectionReason.Outranked, MatchRejectionReason.None], reviewed.Select(candidate => candidate.Rejection?.Reason ?? MatchRejectionReason.None));
        Assert.Equal([("2", MatchRating.DateAndTitleMatches)], linked.Select(candidate => (candidate.ID.ID, candidate.MatchRating)));
        Assert.Equal(["2"], store.Series.Select(x => x.ProviderID));
    }

    // On a forced search of an anime linked to the hint's entry, a lower
    // pick does not remove that link: the hint is taken again and kept.
    [Fact]
    public async Task AReplacingSearchKeepsALinkToAHigherHint()
    {
        var store = new LinkTables();
        var current = SeriesLink("2");
        store.Series.Add(current);
        var (service, _, _, _) = Build(store.Store);
        IReadOnlyList<MetadataAutoLinkCandidate> candidates =
        [
            Candidate("3"),
            Candidate("2") with { Origin = MetadataAutoLinkOrigin.AnidbResource, MatchRating = MatchRating.DateAndTitleMatches },
        ];

        var linked = await service.ApplyAutoLinks(Source, AnimeID, candidates, replace: true, TestContext.Current.CancellationToken);

        Assert.Equal(["2"], linked.Select(candidate => candidate.ID.ID));
        Assert.Same(current, Assert.Single(store.Series));
    }

    // The first one the provider listed that is left.
    [Fact]
    public async Task OnlyOneHintIsTaken()
    {
        var store = new LinkTables();
        var (service, _, _, _) = Build(store.Store);
        IReadOnlyList<MetadataAutoLinkCandidate> candidates =
        [
            Candidate("0", MetadataEntityType.Episode) with { Origin = MetadataAutoLinkOrigin.AnidbResource },
            Candidate("2") with { Origin = MetadataAutoLinkOrigin.AnidbResource },
            Candidate("3") with { Origin = MetadataAutoLinkOrigin.AnidbResource },
        ];

        var reviewed = service.ReviewAutoLinks(Source, AnimeID, [.. candidates.Skip(1)]);
        var linked = await service.ApplyAutoLinks(Source, AnimeID, candidates, replace: false, TestContext.Current.CancellationToken);

        Assert.Equal(["2"], linked.Select(candidate => candidate.ID.ID));
        Assert.Equal(["2"], store.Series.Select(x => x.ProviderID));
        Assert.Equal([null, MatchRejectionReason.HintNotNeeded], reviewed.Select(candidate => candidate.Rejection?.Reason));
    }

    [Fact]
    public async Task AReplacingApplyTakesEveryLinkTheAnimeHadOnTheSource()
    {
        var store = Library();
        var (service, _, _, _) = Build(store.Store);

        var linked = await service.ApplyAutoLinks(Source, AnimeID, [Candidate("3")], replace: true, TestContext.Current.CancellationToken);

        Assert.Single(linked);
        Assert.Equal(["3"], store.Series.Where(x => x.AnidbAnimeID == AnimeID && x.Source == Source).Select(x => x.ProviderID));
        Assert.DoesNotContain(store.Movies, x => x.AnidbAnimeID == AnimeID);
        Assert.DoesNotContain(store.Episodes, x => x.AnidbAnimeID == AnimeID && x.Source == Source);

        // Other anime and other sources are left alone.
        Assert.Contains(store.Series, x => x.AnidbAnimeID == 200);
        Assert.Contains(store.Series, x => x.Source == TestSources.AniList);
        Assert.Contains(store.Movies, x => x.AnidbAnimeID == 200);
    }

    [Fact]
    public async Task AnApplyTakingNothingLeavesEveryLink()
    {
        var store = Library();
        var before = (store.Series.Count, store.Movies.Count, store.Episodes.Count);
        var (service, _, _, _) = Build(store.Store);

        var linked = await service.ApplyAutoLinks(
            Source,
            AnimeID,
            [Candidate("3", rejected: MatchRejectionReason.DateMismatch), Candidate("4", source: TestSources.AniList)],
            replace: true,
            TestContext.Current.CancellationToken
        );

        Assert.Empty(linked);
        Assert.Equal(before, (store.Series.Count, store.Movies.Count, store.Episodes.Count));
    }

    #endregion

    #region Cross-source hints

    private static MetadataGuid Entry(MetadataSource source, MetadataEntityType entityType, string id)
        => new(source, entityType, id);

    // Each entry comes back once with every link naming it: an episode stands for its stored series,
    // a film keeps its link's episode, and the source's own links give nothing.
    [Fact]
    public void CrossSourceHintsAreReadFromTheLinkedEntries()
    {
        var other = TestSources.AniList;
        var store = new LinkTables();
        store.Series.AddRange([
            new() { Source = other, AnidbAnimeID = AnimeID, ProviderID = "10" },
            new() { Source = Source, AnidbAnimeID = AnimeID, ProviderID = "99" },
            new() { Source = other, AnidbAnimeID = 200, ProviderID = "11" },
        ]);
        store.Movies.Add(new() { Source = other, AnidbAnimeID = AnimeID, AnidbEpisodeID = 5, ProviderID = "20" });
        store.Episodes.AddRange([
            new() { Source = other, AnidbAnimeID = AnimeID, AnidbEpisodeID = 1, ProviderID = "30", ProviderParentID = "10" },
            new() { Source = other, AnidbAnimeID = AnimeID, AnidbEpisodeID = 2, ProviderID = "31", ProviderParentID = "10" },
        ]);

        var entries = new Dictionary<MetadataGuid, IMetadata>
        {
            [Entry(other, MetadataEntityType.Series, "10")] = Mock.Of<ISeries>(series => series.CrossSourceIDs == new[]
            {
                Entry(Source, MetadataEntityType.Series, "1"),
                Entry(MetadataSource.TMDB, MetadataEntityType.Series, "5"),
            }),
            [Entry(other, MetadataEntityType.Movie, "20")] = Mock.Of<IMovie>(movie => movie.CrossSourceIDs == new[] { Entry(Source, MetadataEntityType.Movie, "2") }),
            [Entry(other, MetadataEntityType.Episode, "30")] = Mock.Of<IEpisode>(episode => episode.CrossSourceIDs == new[] { Entry(Source, MetadataEntityType.Episode, "300") }),
            [Entry(other, MetadataEntityType.Episode, "31")] = Mock.Of<IEpisode>(episode => episode.CrossSourceIDs == new[] { Entry(Source, MetadataEntityType.Episode, "301") }),
            [Entry(Source, MetadataEntityType.Series, "99")] = Mock.Of<ISeries>(series => series.CrossSourceIDs == new[] { Entry(Source, MetadataEntityType.Series, "98") }),
        };
        var metadata = new Mock<IMetadataService>();
        metadata.Setup(m => m.GetEntry(It.IsAny<MetadataGuid>())).Returns((MetadataGuid id) => entries.GetValueOrDefault(id));
        metadata.Setup(m => m.GetEpisode(Entry(Source, MetadataEntityType.Episode, "300")))
            .Returns(Mock.Of<IEpisode>(episode => episode.SeriesID == Entry(Source, MetadataEntityType.Series, "1")));
        var (service, _, _, _) = Build(store.Store, metadataService: metadata.Object);

        var hints = service.GetCrossSourceHints(Source, AnimeID);

        Assert.Equal(
            [
                (Entry(Source, MetadataEntityType.Series, "1"), (int?)null),
                (Entry(Source, MetadataEntityType.Movie, "2"), 5),
                (Entry(Source, MetadataEntityType.Episode, "301"), null),
            ],
            hints.Select(hint => (hint.ID, hint.AnidbEpisodeID)));
        Assert.Equal(
            [
                [Entry(other, MetadataEntityType.Series, "10"), Entry(other, MetadataEntityType.Episode, "30")],
                [Entry(other, MetadataEntityType.Movie, "20")],
                [Entry(other, MetadataEntityType.Episode, "31")],
            ],
            hints.Select(hint => hint.NamedBy.ToList()));
        Assert.All(hints, hint => Assert.Equal(AnimeID, hint.AnidbAnimeID));
        Assert.Equal(Entry(MetadataSource.TMDB, MetadataEntityType.Series, "5"), Assert.Single(service.GetCrossSourceHints(MetadataSource.TMDB, AnimeID)).ID);
    }

    // One hint at most of either kind, and never verified.
    [Fact]
    public void ACrossSourceHintIsTakenLikeAnAnidbOne()
    {
        var (service, _, _, _) = Build(new LinkTables().Store);

        var reviewed = service.ReviewAutoLinks(Source, AnimeID,
        [
            Candidate("1", rejected: MatchRejectionReason.TitleMismatch),
            Candidate("2") with { Origin = MetadataAutoLinkOrigin.CrossSourceLink, MatchRating = MatchRating.UserVerified },
            Candidate("3") with { Origin = MetadataAutoLinkOrigin.AnidbResource },
        ]);
        var outranked = service.ReviewAutoLinks(Source, AnimeID,
        [
            Candidate("1"),
            Candidate("2") with { Origin = MetadataAutoLinkOrigin.CrossSourceLink, MatchRating = MatchRating.DateAndTitleMatches },
        ]);

        Assert.Equal((null, MatchRating.FirstAvailable), (reviewed[1].Rejection, reviewed[1].MatchRating));
        Assert.Equal(MatchRejectionReason.HintNotNeeded, reviewed[2].Rejection?.Reason);
        Assert.Equal(MatchRejectionReason.Outranked, outranked[0].Rejection?.Reason);
        Assert.Null(outranked[1].Rejection);
    }

    #endregion

    #region Setting an episode link

    [Fact]
    public async Task AnEpisodeLinkRecordsTheSeasonAndNumbersTheStoredEpisodeHas()
    {
        var store = new LinkTables();
        var metadata = new Mock<IMetadataService>();
        var (service, _, _, _) = Build(store.Store, metadata.Object);
        var episode = new Mock<IEpisode>();
        episode.SetupGet(e => e.SeriesID).Returns(new MetadataGuid(Source, MetadataEntityType.Series, "7"));
        episode.SetupGet(e => e.SeasonID).Returns(new MetadataGuid(Source, MetadataEntityType.Season, "70"));
        episode.SetupGet(e => e.SeasonNumber).Returns(2);
        episode.SetupGet(e => e.EpisodeNumber).Returns(5);
        metadata.Setup(m => m.GetEpisode(new MetadataGuid(Source, MetadataEntityType.Episode, "71"))).Returns(episode.Object);

        await service.SetEpisodeLink(EpisodeRequest("71"), TestContext.Current.CancellationToken);

        IMetadataEpisodeCrossReference link = Assert.Single(store.Episodes);
        Assert.Equal(new MetadataGuid(Source, MetadataEntityType.Season, "70"), link.SeasonID);
        Assert.Equal(2, link.SeasonNumber);
        Assert.Equal(5, link.EpisodeNumber);
    }

    [Fact]
    public async Task ALinkToNothingSitsInNoSeries()
    {
        var store = new LinkTables();
        var metadata = new Mock<IMetadataService>();
        var (service, _, _, _) = Build(store.Store, metadata.Object);

        await service.SetEpisodeLink(EpisodeRequest(null), TestContext.Current.CancellationToken);

        IMetadataEpisodeCrossReference link = Assert.Single(store.Episodes);
        Assert.Null(link.ProviderID);
        Assert.Null(link.ProviderParentID);
        metadata.Verify(m => m.GetEpisode(It.IsAny<MetadataGuid>()), Times.Never);
    }

    [Fact]
    public async Task AnEpisodeLinkSitsInTheStoredEpisodesSeriesAndQueuesNoRefresh()
    {
        var store = new LinkTables();
        var metadata = new Mock<IMetadataService>();
        var (service, _, _, queued) = Build(store.Store, metadata.Object);
        var episode = new Mock<IEpisode>();
        episode.SetupGet(e => e.SeriesID).Returns(new MetadataGuid(Source, MetadataEntityType.Series, "7"));
        metadata.Setup(m => m.GetEpisode(new MetadataGuid(Source, MetadataEntityType.Episode, "71"))).Returns(episode.Object);

        await service.SetEpisodeLink(
            EpisodeRequest("71") with { ProviderSeriesID = new MetadataGuid(Source, MetadataEntityType.Series, "8") },
            TestContext.Current.CancellationToken
        );

        // The stored episode's own series wins over the one the request names.
        Assert.Equal("7", Assert.Single(store.Episodes).ProviderParentID);
        Assert.Empty(queued);
    }

    [Fact]
    public async Task AnEpisodeNotStoredYetSitsInTheSeriesTheRequestNames()
    {
        var store = new LinkTables();
        var (service, _, _, queued) = Build(store.Store);
        var series = new MetadataGuid(Source, MetadataEntityType.Series, "8");

        await service.SetEpisodeLink(EpisodeRequest("81") with { ProviderSeriesID = series }, TestContext.Current.CancellationToken);

        Assert.Equal(series, ((IMetadataEpisodeCrossReference)Assert.Single(store.Episodes)).ProviderParentID);
        Assert.Empty(queued);
    }

    [Fact]
    public async Task AnEpisodeInNoKnownSeriesQueuesNothing()
    {
        var store = new LinkTables();
        var (service, _, _, queued) = Build(store.Store);

        await service.SetEpisodeLink(EpisodeRequest("81"), TestContext.Current.CancellationToken);

        Assert.Null(((IMetadataEpisodeCrossReference)Assert.Single(store.Episodes)).ProviderParentID);
        Assert.Empty(queued);
    }

    [Fact]
    public async Task ASeriesTheRequestNamesOnAnotherSourceIsRefused()
    {
        var store = new LinkTables();
        var (service, _, _, _) = Build(store.Store);

        await Assert.ThrowsAsync<ArgumentException>(() => service.SetEpisodeLink(
            EpisodeRequest("81") with { ProviderSeriesID = new MetadataGuid(TestSources.AniList, MetadataEntityType.Series, "8") },
            TestContext.Current.CancellationToken
        ));
        Assert.Empty(store.Episodes);
    }

    [Fact]
    public async Task ARequestNamingAnotherSourceOrKindIsRefused()
    {
        var store = new LinkTables();
        var (service, _, _, _) = Build(store.Store);

        await Assert.ThrowsAsync<ArgumentException>(() => service.SetEpisodeLink(
            EpisodeRequest(null) with { ProviderID = new MetadataGuid(TestSources.AniList, MetadataEntityType.Episode, "1") },
            TestContext.Current.CancellationToken
        ));
        await Assert.ThrowsAsync<ArgumentException>(() => service.AddSeriesLink(
            SeriesRequest("1") with { ProviderID = new MetadataGuid(Source, MetadataEntityType.Episode, "1") },
            TestContext.Current.CancellationToken
        ));
        Assert.Empty(store.Episodes);
        Assert.Empty(store.Series);
    }

    [Fact]
    public async Task AKeptEpisodeLinkIsPlacedWhereTheRequestSays()
    {
        var store = new LinkTables();
        store.Episodes.AddRange([EpisodeLink(1, "11", "1"), EpisodeLink(1, "12", "1")]);
        store.Episodes[1].Ordering = 1;
        var (service, _, _, _) = Build(store.Store);

        await service.SetEpisodeLink(EpisodeRequest("13") with { Ordering = 0 }, TestContext.Current.CancellationToken);

        Assert.Equal(["13", "11", "12"], store.Episodes.OrderBy(x => x.Ordering).Select(x => x.ProviderID));
    }

    [Fact]
    public async Task ALinkToNothingReplacesTheEpisodesOtherLinks()
    {
        var store = new LinkTables();
        store.Episodes.AddRange([EpisodeLink(1, "11", "1"), EpisodeLink(1, "12", "1"), EpisodeLink(2, "21", "1")]);
        var (service, _, _, _) = Build(store.Store);

        await service.SetEpisodeLink(EpisodeRequest(null) with { Additive = true }, TestContext.Current.CancellationToken);

        Assert.Equal(string.Empty, Assert.Single(store.Episodes, x => x.AnidbEpisodeID is 1).ProviderID);
        Assert.Equal("21", Assert.Single(store.Episodes, x => x.AnidbEpisodeID is 2).ProviderID);
    }

    [Fact]
    public async Task AKeptEpisodeLinkTakesThePlaceOfALinkToNothing()
    {
        var store = new LinkTables();
        store.Episodes.AddRange([EpisodeLink(1, string.Empty, string.Empty), EpisodeLink(2, string.Empty, string.Empty)]);
        var (service, _, _, _) = Build(store.Store);

        await service.SetEpisodeLink(EpisodeRequest("13") with { Additive = true }, TestContext.Current.CancellationToken);

        Assert.Equal("13", Assert.Single(store.Episodes, x => x.AnidbEpisodeID is 1).ProviderID);
        Assert.Equal(string.Empty, Assert.Single(store.Episodes, x => x.AnidbEpisodeID is 2).ProviderID);
    }

    private static MetadataSeriesLinkRequest SeriesRequest(string providerID, MetadataEntityType? entityType = null)
        => new()
        {
            Source = Source,
            EntityType = entityType ?? MetadataEntityType.Series,
            AnidbAnimeID = AnimeID,
            ProviderID = new MetadataGuid(Source, entityType ?? MetadataEntityType.Series, providerID),
        };

    private static MetadataEpisodeLinkRequest EpisodeRequest(string? providerID)
        => new()
        {
            Source = Source,
            EntityType = MetadataEntityType.Episode,
            AnidbAnimeID = AnimeID,
            AnidbEpisodeID = 1,
            ProviderID = providerID is null ? null : new MetadataGuid(Source, MetadataEntityType.Episode, providerID),
        };

    #endregion

    #region Matching episodes

    [Fact]
    public async Task MatchingHandsTheProviderItsSeriesAndSeasonAsMetadataIDs()
    {
        var store = new LinkTables();
        var anime = CachedRepo.Build<AniDB_AnimeRepository, int, AniDB_Anime>(row => row.AniDB_AnimeID, new AniDB_Anime { AniDB_AnimeID = 1, AnimeID = AnimeID });
        var episodes = CachedRepo.Build<AniDB_EpisodeRepository, int, AniDB_Episode>(
            row => row.AniDB_EpisodeID,
            new AniDB_Episode { AniDB_EpisodeID = 1, EpisodeID = 11, AnimeID = AnimeID, EpisodeType = EpisodeType.Episode },
            new AniDB_Episode { AniDB_EpisodeID = 2, EpisodeID = 12, AnimeID = AnimeID, EpisodeType = EpisodeType.Credits }
        );
        var (service, provider, _, _) = Build(store.Store, anidbAnime: anime, anidbEpisodes: episodes);
        var series = new MetadataGuid(Source, MetadataEntityType.Series, "s1");
        var season = new MetadataGuid(Source, MetadataEntityType.Season, "s1-2");
        MetadataGuid? askedSeries = null;
        MetadataGuid? askedSeason = null;
        IReadOnlyList<IAnidbEpisode>? askedEpisodes = null;
        provider
            .Setup(p => p.MatchEpisodes(
                It.IsAny<IAnidbAnime>(),
                It.IsAny<IReadOnlyList<IAnidbEpisode>>(),
                It.IsAny<MetadataGuid>(),
                It.IsAny<MetadataGuid?>(),
                It.IsAny<IReadOnlyList<IMetadataEpisodeCrossReference>?>(),
                It.IsAny<bool?>(),
                It.IsAny<CancellationToken>()
            ))
            .Callback((IAnidbAnime _, IReadOnlyList<IAnidbEpisode> eps, MetadataGuid seriesID, MetadataGuid? seasonID, IReadOnlyList<IMetadataEpisodeCrossReference>? _,
                bool? _, CancellationToken _) =>
            {
                askedEpisodes = eps;
                askedSeries = seriesID;
                askedSeason = seasonID;
            })
            .ReturnsAsync([]);

        await service.MatchEpisodes(AnimeID, series, season, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(series, askedSeries);
        Assert.Equal(season, askedSeason);
        Assert.Equal([11], askedEpisodes!.Select(episode => episode.AnidbID));
    }

    /// <summary>
    /// Sets the provider up to answer a match with the given matches.
    /// </summary>
    private static void AnswerMatches(Mock<IMetadataSeriesLinkingProvider> provider, IReadOnlyList<EpisodeMatch> matches)
        => provider
            .Setup(p => p.MatchEpisodes(
                It.IsAny<IAnidbAnime>(),
                It.IsAny<IReadOnlyList<IAnidbEpisode>>(),
                It.IsAny<MetadataGuid>(),
                It.IsAny<MetadataGuid?>(),
                It.IsAny<IReadOnlyList<IMetadataEpisodeCrossReference>?>(),
                It.IsAny<bool?>(),
                It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync(matches);

    private static AniDB_AnimeRepository Anime()
        => CachedRepo.Build<AniDB_AnimeRepository, int, AniDB_Anime>(row => row.AniDB_AnimeID, new AniDB_Anime { AniDB_AnimeID = 1, AnimeID = AnimeID });

    private static IEpisode Candidate(MetadataGuid series, string episodeID)
    {
        var candidate = new Mock<IEpisode>();
        candidate.SetupGet(e => e.ID).Returns(new MetadataGuid(Source, MetadataEntityType.Episode, episodeID));
        candidate.SetupGet(e => e.SeriesID).Returns(series);
        return candidate.Object;
    }

    [Fact]
    public async Task SavingAMatchReplacesTheLinksOfTheEpisodesItNames()
    {
        var store = new LinkTables();
        store.Episodes.Add(new()
        {
            Source = Source,
            AnidbAnimeID = AnimeID,
            AnidbEpisodeID = 11,
            ProviderID = "old",
            ProviderParentID = "s1",
            MatchRating = MatchRating.FirstAvailable,
        });
        store.Episodes.Add(new()
        {
            Source = Source,
            AnidbAnimeID = AnimeID,
            AnidbEpisodeID = 12,
            ProviderID = "kept",
            ProviderParentID = "s1",
            MatchRating = MatchRating.UserVerified,
        });
        var anime = Anime();
        var anidbEpisode = new AniDB_Episode { AniDB_EpisodeID = 1, EpisodeID = 11, AnimeID = AnimeID, EpisodeType = EpisodeType.Episode };
        var episodes = CachedRepo.Build<AniDB_EpisodeRepository, int, AniDB_Episode>(
            row => row.AniDB_EpisodeID,
            anidbEpisode,
            new AniDB_Episode { AniDB_EpisodeID = 2, EpisodeID = 12, AnimeID = AnimeID, EpisodeType = EpisodeType.Episode }
        );
        var (service, provider, _, queued) = Build(store.Store, anidbAnime: anime, anidbEpisodes: episodes);
        var series = new MetadataGuid(Source, MetadataEntityType.Series, "s1");
        AnswerMatches(provider,
        [
            new EpisodeMatch { AnidbEpisode = anidbEpisode, Candidate = Candidate(series, "new"), Rating = MatchRating.DateAndTitleMatches },
        ]);

        await service.MatchEpisodes(AnimeID, series, useExisting: true, save: true, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("new", Assert.Single(store.Episodes, x => x.AnidbEpisodeID == 11).ProviderID);
        Assert.Equal("kept", Assert.Single(store.Episodes, x => x.AnidbEpisodeID == 12).ProviderID);
        Assert.Empty(queued);
    }

    [Fact]
    public async Task APreviewCarriesThePlaceTheSourceGave()
    {
        var store = new LinkTables();
        var anime = Anime();
        var anidbEpisode = new AniDB_Episode { AniDB_EpisodeID = 1, EpisodeID = 11, AnimeID = AnimeID, EpisodeType = EpisodeType.Episode };
        var episodes = CachedRepo.Build<AniDB_EpisodeRepository, int, AniDB_Episode>(row => row.AniDB_EpisodeID, anidbEpisode);
        var (service, provider, _, queued) = Build(store.Store, anidbAnime: anime, anidbEpisodes: episodes);
        var series = new MetadataGuid(Source, MetadataEntityType.Series, "s1");
        AnswerMatches(provider,
        [
            new EpisodeMatch { AnidbEpisode = anidbEpisode, Candidate = Candidate(series, "a"), Rating = MatchRating.TitleMatches, Ordering = 0 },
            new EpisodeMatch { AnidbEpisode = anidbEpisode, Candidate = Candidate(series, "b"), Rating = MatchRating.TitleMatches, Ordering = 1 },
        ]);

        var preview = await service.MatchEpisodes(AnimeID, series, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal([("a", 0), ("b", 1)], preview.Select(link => (link.ProviderID!.ID, link.Ordering)));
        Assert.Empty(store.Episodes);
        Assert.Empty(queued);
    }

    #endregion

    #region Matching again

    private static readonly DateOnly s_firstAired = new(2024, 1, 7);

    private static IAnidbEpisode AiredAnidbEpisode(int anidbEpisodeID, int number)
    {
        var episode = new Mock<IAnidbEpisode>();
        episode.SetupGet(e => e.AnidbID).Returns(anidbEpisodeID);
        episode.SetupGet(e => e.AnidbAnimeID).Returns(AnimeID);
        episode.SetupGet(e => e.ID).Returns(new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Episode, anidbEpisodeID.ToString()));
        episode.SetupGet(e => e.Type).Returns(EpisodeType.Episode);
        episode.SetupGet(e => e.EpisodeNumber).Returns(number);
        episode.SetupGet(e => e.AirDate).Returns(s_firstAired.AddDays(7 * (number - 1)));
        episode.SetupGet(e => e.Titles).Returns([]);
        episode.SetupGet(e => e.ShokoEpisodes).Returns([]);
        return episode.Object;
    }

    private static IEpisode AiredCandidate(MetadataGuid series, int number)
    {
        var candidate = new Mock<IEpisode>();
        candidate.SetupGet(e => e.ID).Returns(new MetadataGuid(Source, MetadataEntityType.Episode, $"e{number}"));
        candidate.SetupGet(e => e.SeriesID).Returns(series);
        candidate.SetupGet(e => e.Type).Returns(EpisodeType.Episode);
        candidate.SetupGet(e => e.EpisodeNumber).Returns(number);
        candidate.SetupGet(e => e.AirDate).Returns(s_firstAired.AddDays(7 * (number - 1)));
        candidate.SetupGet(e => e.Titles).Returns([]);
        return candidate.Object;
    }

    private static CrossRef_AniDB_Metadata_Episode StoredEpisodeLink(int anidbEpisodeID, int? number, MatchRating rating)
        => new()
        {
            Source = Source,
            AnidbAnimeID = AnimeID,
            AnidbEpisodeID = anidbEpisodeID,
            ProviderID = number is null ? string.Empty : $"e{number}",
            ProviderParentID = number is null ? string.Empty : "s1",
            EpisodeNumber = number,
            MatchRating = rating,
        };

    [Fact]
    public async Task MatchingAgainFillsWhatTheMatchingLeftEmpty_KeepsAPersonsLinksAndRefusals_AndWritesNothingTheSecondTime()
    {
        var store = new LinkTables();
        store.Series.Add(SeriesLink("s1"));
        store.Episodes.AddRange([
            StoredEpisodeLink(11, 1, MatchRating.DateAndNumberMatches),
            StoredEpisodeLink(12, 2, MatchRating.UserVerified),
            StoredEpisodeLink(13, null, MatchRating.None),
            StoredEpisodeLink(14, null, MatchRating.UserVerified),
        ]);
        var (service, provider, _, _) = Build(store.Store, anidbAnime: Anime());
        var series = new MetadataGuid(Source, MetadataEntityType.Series, "s1");
        var anidbEpisodes = Enumerable.Range(1, 4).Select(number => AiredAnidbEpisode(10 + number, number)).ToList();
        var candidates = Enumerable.Range(1, 4).Select(number => AiredCandidate(series, number)).ToList();
        var engine = new MetadataMatchingEngine(
            NullLogger<MetadataMatchingEngine>.Instance,
            new FuzzySearchService(),
            MetadataMatchingEngineTests.LookAhead()
        );
        provider
            .Setup(p => p.MatchEpisodes(
                It.IsAny<IAnidbAnime>(),
                It.IsAny<IReadOnlyList<IAnidbEpisode>>(),
                series,
                It.IsAny<MetadataGuid?>(),
                It.IsAny<IReadOnlyList<IMetadataEpisodeCrossReference>?>(),
                It.IsAny<bool?>(),
                It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync((
                IAnidbAnime _,
                IReadOnlyList<IAnidbEpisode> _,
                MetadataGuid _,
                MetadataGuid? _,
                IReadOnlyList<IMetadataEpisodeCrossReference>? existing,
                bool? _,
                CancellationToken _
            ) => engine.MatchEpisodes(anidbEpisodes, candidates, existing, new() { Strategy = EpisodeMatchStrategy.DateThenNumber }));
        var token = TestContext.Current.CancellationToken;
        var before = store.Episodes.ToDictionary(link => link.AnidbEpisodeID, link => link.CrossRef_AniDB_Metadata_EpisodeID);

        Assert.Equal(1, await service.RematchEpisodes(Source, AnimeID, token));
        Assert.Equal(0, await service.RematchEpisodes(Source, AnimeID, token));

        Assert.Equal(
            [
                (11, "e1", MatchRating.DateAndNumberMatches),
                (12, "e2", MatchRating.UserVerified),
                (13, "e3", MatchRating.DateAndNumberMatches),
                (14, "", MatchRating.UserVerified),
            ],
            store.Episodes.OrderBy(link => link.AnidbEpisodeID).Select(link => (link.AnidbEpisodeID, link.ProviderID, link.MatchRating))
        );
        Assert.Equal(before[11], Assert.Single(store.Episodes, link => link.AnidbEpisodeID == 11).CrossRef_AniDB_Metadata_EpisodeID);
        Assert.Equal(before[14], Assert.Single(store.Episodes, link => link.AnidbEpisodeID == 14).CrossRef_AniDB_Metadata_EpisodeID);
    }

    [Fact]
    public async Task MatchingAgainLeavesAnEpisodeLinkedIntoAnotherLinkedSeriesToThatSeries()
    {
        var store = new LinkTables();
        store.Series.AddRange([SeriesLink("s1"), SeriesLink("s2")]);
        var linked = StoredEpisodeLink(11, 1, MatchRating.FirstAvailable);
        linked.ProviderParentID = "s2";
        store.Episodes.Add(linked);
        var anidbEpisode = new AniDB_Episode { AniDB_EpisodeID = 1, EpisodeID = 11, AnimeID = AnimeID, EpisodeType = EpisodeType.Episode };
        var episodes = CachedRepo.Build<AniDB_EpisodeRepository, int, AniDB_Episode>(row => row.AniDB_EpisodeID, anidbEpisode);
        var (service, provider, _, _) = Build(store.Store, anidbAnime: Anime(), anidbEpisodes: episodes);
        provider
            .Setup(p => p.MatchEpisodes(
                It.IsAny<IAnidbAnime>(),
                It.IsAny<IReadOnlyList<IAnidbEpisode>>(),
                It.IsAny<MetadataGuid>(),
                It.IsAny<MetadataGuid?>(),
                It.IsAny<IReadOnlyList<IMetadataEpisodeCrossReference>?>(),
                It.IsAny<bool?>(),
                It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync((
                IAnidbAnime _,
                IReadOnlyList<IAnidbEpisode> _,
                MetadataGuid series,
                MetadataGuid? _,
                IReadOnlyList<IMetadataEpisodeCrossReference>? _,
                bool? _,
                CancellationToken _
            ) =>
            [
                new EpisodeMatch
                {
                    AnidbEpisode = anidbEpisode,
                    Candidate = AiredCandidate(series, series.ID == "s1" ? 2 : 1),
                    Rating = MatchRating.FirstAvailable,
                },
            ]);

        Assert.Equal(0, await service.RematchEpisodes(Source, AnimeID, TestContext.Current.CancellationToken));

        Assert.Equal("e1", Assert.Single(store.Episodes).ProviderID);
    }

    #endregion

    #region Resetting episode links

    private static AniDB_EpisodeRepository ResetEpisodes()
        => CachedRepo.Build<AniDB_EpisodeRepository, int, AniDB_Episode>(
            row => row.AniDB_EpisodeID,
            new AniDB_Episode { AniDB_EpisodeID = 1, EpisodeID = 11, AnimeID = AnimeID, EpisodeType = EpisodeType.Episode },
            new AniDB_Episode { AniDB_EpisodeID = 2, EpisodeID = 12, AnimeID = AnimeID, EpisodeType = EpisodeType.Special },
            new AniDB_Episode { AniDB_EpisodeID = 3, EpisodeID = 13, AnimeID = AnimeID, EpisodeType = EpisodeType.Credits }
        );

    [Fact]
    public async Task RefusingMatchingLeavesEachEpisodeDeliberatelyUnlinked()
    {
        var store = new LinkTables();
        store.Series.Add(SeriesLink("s1"));
        store.Episodes.Add(EpisodeLink(11, "e1", "s1"));
        var (service, _, _, _) = Build(store.Store, anidbEpisodes: ResetEpisodes());

        await service.ResetEpisodeLinks(Source, AnimeID, allowAutoMatch: false, TestContext.Current.CancellationToken);

        Assert.Equal([11, 12], store.Episodes.Select(x => x.AnidbEpisodeID).Order());
        Assert.All(store.Episodes, x =>
        {
            Assert.Equal(string.Empty, x.ProviderID);
            Assert.Equal(MatchRating.UserVerified, x.MatchRating);
        });
    }

    [Fact]
    public async Task AllowingMatchingClearsTheEpisodeLinks()
    {
        var store = new LinkTables();
        store.Series.Add(SeriesLink("s1"));
        store.Episodes.Add(EpisodeLink(11, "e1", "s1"));
        var (service, _, _, _) = Build(store.Store, anidbEpisodes: ResetEpisodes());

        await service.ResetEpisodeLinks(Source, AnimeID, allowAutoMatch: true, TestContext.Current.CancellationToken);

        Assert.Empty(store.Episodes);
    }

    [Fact]
    public async Task WithNoSeriesLinkedNothingIsKeptOnTheEpisodes()
    {
        var store = new LinkTables();
        store.Episodes.Add(EpisodeLink(11, "e1", "s1"));
        var (service, _, _, _) = Build(store.Store, anidbEpisodes: ResetEpisodes());

        await service.ResetEpisodeLinks(Source, AnimeID, allowAutoMatch: false, TestContext.Current.CancellationToken);

        Assert.Empty(store.Episodes);
    }

    [Fact]
    public async Task RemovingNothingDoesNotTellTheSourceToLeaveTheAnimeAlone()
    {
        var store = new LinkTables();
        var shokoSeries = new AnimeSeries { AnimeSeriesID = 1, AniDB_ID = AnimeID };
        var animeSeries = CachedRepo.Build<AnimeSeriesRepository, int, AnimeSeries>(series => series.AnimeSeriesID, shokoSeries);
        var (service, _, _, _) = Build(store.Store, anidbEpisodes: ResetEpisodes(), animeSeries: animeSeries);

        var token = TestContext.Current.CancellationToken;
        var removed = await service.RemoveLinksForAnime(Source, AnimeID, disableAutoLinking: true, cancellationToken: token);
        var removedForEpisode = await service.RemoveLinksForEpisode(Source, 11, disableAutoLinking: true, cancellationToken: token);

        Assert.Equal(0, removed);
        Assert.Equal(0, removedForEpisode);
        Assert.False(shokoSeries.IsAutoLinkingDisabled(Source));
    }

    #endregion

    #region Match ratings

    private static readonly Guid s_writer = Guid.NewGuid();

    /// <summary>
    /// A real store holding a series link, a film claiming the whole anime, a
    /// film link and two episode links, all automatic and written by one
    /// provider, reporting later writes to the tracker given.
    /// </summary>
    /// <param name="linkChanges">Where the store reports the links it changed.</param>
    private static async Task<WritableLinkStore> RatedLibrary(MetadataLinkChangeTracker? linkChanges = null)
    {
        var links = new WritableLinkStore(linkChanges);
        var options = new MetadataLinkUpdateOptions { WrittenBy = s_writer };
        var cancellationToken = TestContext.Current.CancellationToken;
        await links.Store.MergeSeriesLinks(
            [
                new() { Source = Source, AnidbAnimeID = AnimeID, ProviderID = new(Source, MetadataEntityType.Series, "1"), MatchRating = MatchRating.TitleMatches },
                new() { Source = Source, AnidbAnimeID = AnimeID, ProviderID = new(Source, MetadataEntityType.Series, "2"), MatchRating = MatchRating.FirstAvailable },
                new() { Source = Source, AnidbAnimeID = AnimeID, ProviderID = new(Source, MetadataEntityType.Movie, "9"), MatchRating = MatchRating.TitleMatches },
            ],
            options: options,
            cancellationToken: cancellationToken
        );
        await links.Store.MergeMovieLinks(
            [new() { Source = Source, AnidbAnimeID = AnimeID, AnidbEpisodeID = 5, ProviderID = new(Source, MetadataEntityType.Movie, "8"), MatchRating = MatchRating.DateMatches }],
            options: options,
            cancellationToken: cancellationToken
        );
        await links.Store.MergeEpisodeLinks(
            [
                new()
                {
                    Source = Source, AnidbAnimeID = AnimeID, AnidbEpisodeID = 1, ProviderID = new(Source, MetadataEntityType.Episode, "11"),
                    ProviderParentID = new(Source, MetadataEntityType.Series, "1"), SeasonNumber = 1, EpisodeNumber = 1, MatchRating = MatchRating.DateMatches,
                },
                new() { Source = Source, AnidbAnimeID = AnimeID, AnidbEpisodeID = 2, ProviderID = null, MatchRating = MatchRating.None },
            ],
            options: options,
            cancellationToken: cancellationToken
        );
        return links;
    }

    [Fact]
    public async Task VerifyingASeriesLinkChangesOnlyItsRating()
    {
        var links = await RatedLibrary();
        var (service, _, _, _) = Build(links.Store);
        var link = links.Store.GetSeriesLinks(AnimeID, Source)[1];

        var updated = await service.SetMatchRating([link], cancellationToken: TestContext.Current.CancellationToken);

        var verified = Assert.Single(updated);
        Assert.Equal(("2", MatchRating.UserVerified, 1, s_writer), (verified.ProviderID!.ID, verified.MatchRating, verified.Ordering, verified.WrittenBy));
        // Nothing else moved, went or was verified.
        Assert.Equal(
            [("1", MatchRating.TitleMatches), ("2", MatchRating.UserVerified), ("9", MatchRating.TitleMatches)],
            links.Store.GetSeriesLinks(AnimeID, Source).Select(x => (x.ProviderID!.ID, x.MatchRating))
        );
        Assert.Equal(2, links.Store.GetEpisodeLinksForSeries(AnimeID, Source).Count);
        Assert.Single(links.Store.GetMovieLinksForSeries(AnimeID, Source));
    }

    [Fact]
    public async Task FilmAndEpisodeLinksAreRatedInPlaceKeepingWhereTheyPoint()
    {
        var links = await RatedLibrary();
        var (service, _, _, _) = Build(links.Store);

        var updated = await service.SetMatchRating(
            [links.Store.GetMovieLinks(5, Source)[0], .. links.Store.GetEpisodeLinksForSeries(AnimeID, Source)],
            MatchRating.TitleKindaMatches,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(3, updated.Count);
        Assert.All(updated, x => Assert.Equal(MatchRating.TitleKindaMatches, x.MatchRating));
        var episode = links.Store.GetEpisodeLinks(1, Source)[0];
        Assert.Equal(("1", 1, 1, s_writer), (episode.ProviderParentID!.ID, episode.SeasonNumber, episode.EpisodeNumber, episode.WrittenBy));
        Assert.Null(links.Store.GetEpisodeLinks(2, Source)[0].ProviderID);
        Assert.Equal(MatchRating.TitleKindaMatches, links.Store.GetMovieLinks(5, Source)[0].MatchRating);
    }

    [Fact]
    public async Task AFilmClaimingTheWholeAnimeIsVerifiedAtTheSeriesLevel()
    {
        var links = await RatedLibrary();
        var (service, _, _, _) = Build(links.Store);
        var film = links.Store.GetLinksTo(new(Source, MetadataEntityType.Movie, "9"));

        var updated = await service.SetMatchRating(film, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(MetadataEntityType.Series, Assert.Single(updated).EntityType);
        Assert.Equal(MatchRating.UserVerified, links.Store.GetSeriesLinks(AnimeID, Source).Single(x => x.ProviderID!.ID == "9").MatchRating);
        Assert.Equal(MatchRating.TitleMatches, links.Store.GetSeriesLinks(AnimeID, Source).Single(x => x.ProviderID!.ID == "1").MatchRating);
    }

    [Fact]
    public async Task ALinkNoLongerStoredIsSkippedAndNothingIsAdded()
    {
        var links = await RatedLibrary();
        var (service, _, _, _) = Build(links.Store);
        var gone = new CrossRef_AniDB_Metadata_Series { Source = Source, AnidbAnimeID = AnimeID, ProviderID = "7" };

        var updated = await service.SetMatchRating([gone], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(updated);
        Assert.Equal(["1", "2", "9"], links.Store.GetSeriesLinks(AnimeID, Source).Select(x => x.ProviderID!.ID));
    }

    [Fact]
    public async Task RatingsAreSetWhileTheSourcesProviderIsOff()
    {
        var links = await RatedLibrary();
        var (service, _, _, _) = Build(links.Store, providersOff: true);

        await service.SetMatchRating(links.Store.GetSeriesLinks(AnimeID, Source), cancellationToken: TestContext.Current.CancellationToken);

        Assert.All(links.Store.GetSeriesLinks(AnimeID, Source), x => Assert.Equal(MatchRating.UserVerified, x.MatchRating));
    }

    [Fact]
    public async Task AnUndefinedRatingIsRefused()
    {
        var links = await RatedLibrary();
        var (service, _, _, _) = Build(links.Store);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.SetMatchRating(links.Store.GetSeriesLinks(AnimeID, Source), (MatchRating)6, TestContext.Current.CancellationToken));
        Assert.Equal(MatchRating.TitleMatches, links.Store.GetSeriesLinks(AnimeID, Source)[0].MatchRating);
    }

    #endregion

    #region Link events

    /// <summary>
    /// A real store and the service over it, both reporting to one tracker,
    /// with every event the service raises collected.
    /// </summary>
    private sealed class EventedLinks
    {
        public MetadataLinkChangeTracker Tracker { get; } = new();

        public WritableLinkStore Links { get; }

        public MetadataLinkingService Service { get; }

        public List<MetadataLinksChangedEventArgs> Raised { get; } = [];

        public EventedLinks(WritableLinkStore? links = null)
        {
            Links = links ?? new WritableLinkStore(Tracker);
            Service = Build(Links.Store, linkChanges: Tracker).Service;
            Service.LinksChanged += (sender, eventArgs) =>
            {
                Assert.Same(Service, sender);
                Raised.Add(eventArgs);
            };
        }
    }

    [Fact]
    public async Task AddingASeriesLinkRaisesOneEvent()
    {
        var evented = new EventedLinks();

        await evented.Service.AddSeriesLink(SeriesRequest("1"), TestContext.Current.CancellationToken);

        var raised = Assert.Single(evented.Raised);
        Assert.Equal(MetadataLinkChangeReason.Other, raised.Reason);
        var change = Assert.Single(raised.Changes);
        Assert.Equal(
            (MetadataLinkChangeKind.Added, MetadataEntityType.Series, AnimeID, (int?)null, "1", (MatchRating?)null),
            (change.Kind, change.EntityType, change.AnidbAnimeID, change.AnidbEpisodeID, change.ProviderID!.ID, change.PreviousMatchRating)
        );
        Assert.Equal([Source], raised.Sources);
        Assert.Equal([AnimeID], raised.AnidbAnimeIDs);
    }

    // A replacing link names the series it took the place of, and the
    // episode links it took along come in the same event.
    [Fact]
    public async Task ReplacingASeriesLinkIsOneReplacedChange()
    {
        var evented = new EventedLinks();
        var cancellationToken = TestContext.Current.CancellationToken;
        await evented.Service.AddSeriesLink(SeriesRequest("1"), cancellationToken);
        await evented.Links.Store.MergeEpisodeLinks(
            [new() { Source = Source, AnidbAnimeID = AnimeID, AnidbEpisodeID = 1, ProviderID = new(Source, MetadataEntityType.Episode, "11"), ProviderParentID = new(Source, MetadataEntityType.Series, "1") }],
            cancellationToken: cancellationToken
        );
        evented.Raised.Clear();

        await evented.Service.AddSeriesLink(SeriesRequest("2") with { Additive = false, MatchRating = MatchRating.TitleMatches }, cancellationToken);

        var raised = Assert.Single(evented.Raised);
        Assert.Equal(
            [
                (MetadataLinkChangeKind.Replaced, MetadataEntityType.Series, "2", "1"),
                (MetadataLinkChangeKind.Removed, MetadataEntityType.Episode, "11", null),
            ],
            raised.Changes.Select(change => (change.Kind, change.EntityType, change.ProviderID!.ID, change.PreviousProviderID?.ID))
        );
        Assert.Equal((MatchRating.TitleMatches, MatchRating.UserVerified), (raised.Changes[0].MatchRating, raised.Changes[0].PreviousMatchRating));
    }

    // Verifying keeps its own reason, even when an API call asked for it.
    [Fact]
    public async Task VerifyingRaisesRatingChanges()
    {
        var evented = new EventedLinks();
        var links = await RatedLibrary(evented.Tracker);
        var service = Build(links.Store, linkChanges: evented.Tracker).Service;
        var raised = new List<MetadataLinksChangedEventArgs>();
        service.LinksChanged += (_, eventArgs) => raised.Add(eventArgs);

        using (evented.Tracker.UseDefaultReason(MetadataLinkChangeReason.Manual))
            await service.SetMatchRating(links.Store.GetSeriesLinks(AnimeID, Source), cancellationToken: TestContext.Current.CancellationToken);

        var verified = Assert.Single(raised);
        Assert.Equal(MetadataLinkChangeReason.Verify, verified.Reason);
        Assert.Equal(
            [
                ("1", MatchRating.TitleMatches),
                ("2", MatchRating.FirstAvailable),
                ("9", MatchRating.TitleMatches),
            ],
            verified.Changes.Select(change => (change.ProviderID!.ID, change.PreviousMatchRating!.Value))
        );
        Assert.All(verified.Changes, change => Assert.Equal((MetadataLinkChangeKind.RatingChanged, MatchRating.UserVerified), (change.Kind, change.MatchRating!.Value)));
    }

    [Theory]
    [InlineData(false, MetadataLinkChangeReason.AutoLink)]
    [InlineData(true, MetadataLinkChangeReason.ForcedResearch)]
    public async Task AnAutoLinkIsOneEventOfItsOwnReason(bool replace, MetadataLinkChangeReason reason)
    {
        var episodes = CachedRepo.Build<AniDB_EpisodeRepository, int, AniDB_Episode>(
            row => row.AniDB_EpisodeID,
            new AniDB_Episode { AniDB_EpisodeID = 1, EpisodeID = 5, AnimeID = AnimeID, EpisodeType = EpisodeType.Episode }
        );
        var tracker = new MetadataLinkChangeTracker();
        var links = new WritableLinkStore(tracker);
        await links.Store.MergeSeriesLinks([new() { Source = Source, AnidbAnimeID = AnimeID, ProviderID = new(Source, MetadataEntityType.Series, "4") }],
            cancellationToken: TestContext.Current.CancellationToken);
        var service = Build(links.Store, anidbEpisodes: episodes, linkChanges: tracker).Service;
        var raised = new List<MetadataLinksChangedEventArgs>();
        service.LinksChanged += (_, eventArgs) => raised.Add(eventArgs);

        await service.ApplyAutoLinks(
            Source,
            AnimeID,
            [Candidate("1"), Candidate("8", MetadataEntityType.Movie, anidbEpisodeID: 5)],
            replace,
            TestContext.Current.CancellationToken
        );

        var linked = Assert.Single(raised);
        Assert.Equal(reason, linked.Reason);
        Assert.Equal(
            replace
                ? [(MetadataLinkChangeKind.Replaced, "1"), (MetadataLinkChangeKind.Added, "8")]
                : [(MetadataLinkChangeKind.Added, "1"), (MetadataLinkChangeKind.Added, "8")],
            linked.Changes.Select(change => (change.Kind, change.ProviderID!.ID))
        );
    }

    [Fact]
    public async Task ABulkRemovalIsOneEvent()
    {
        var tracker = new MetadataLinkChangeTracker();
        var links = await RatedLibrary(tracker);
        var service = Build(links.Store, linkChanges: tracker).Service;
        var raised = new List<MetadataLinksChangedEventArgs>();
        service.LinksChanged += (_, eventArgs) => raised.Add(eventArgs);

        var removed = await service.RemoveLinksForAnime(Source, AnimeID, cancellationToken: TestContext.Current.CancellationToken);

        var removal = Assert.Single(raised);
        Assert.Equal(removed, removal.Changes.Count);
        Assert.All(removal.Changes, change => Assert.Equal(MetadataLinkChangeKind.Removed, change.Kind));
        Assert.Equal(
            [MetadataEntityType.Series, MetadataEntityType.Movie, MetadataEntityType.Episode],
            removal.Changes.Select(change => change.EntityType).Distinct()
        );
    }

    // A plugin writing to the store straight is heard through the service.
    [Fact]
    public async Task AStoreWriteOutsideTheServiceIsReported()
    {
        var evented = new EventedLinks();

        await evented.Links.Store.MergeMovieLinks(
            [new() { Source = Source, AnidbAnimeID = AnimeID, AnidbEpisodeID = 5, ProviderID = new(Source, MetadataEntityType.Movie, "8"), MatchRating = MatchRating.DateMatches }],
            cancellationToken: TestContext.Current.CancellationToken
        );
        await evented.Links.Store.RemoveLinksForSeries(Source, AnimeID, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(
            [(MetadataLinkChangeKind.Added, MetadataEntityType.Movie, 5), (MetadataLinkChangeKind.Removed, MetadataEntityType.Movie, 5)],
            evented.Raised.Select(raised => Assert.Single(raised.Changes)).Select(change => (change.Kind, change.EntityType, change.AnidbEpisodeID!.Value))
        );
        Assert.All(evented.Raised, raised => Assert.Equal(MetadataLinkChangeReason.Other, raised.Reason));
    }

    [Fact]
    public async Task NothingChangedRaisesNothingAndAFailingHandlerIsContained()
    {
        var evented = new EventedLinks();
        var cancellationToken = TestContext.Current.CancellationToken;
        await evented.Service.AddSeriesLink(SeriesRequest("1"), cancellationToken);
        evented.Raised.Clear();
        var reached = 0;
        evented.Service.LinksChanged += (_, _) => throw new InvalidOperationException("A handler failed.");
        evented.Service.LinksChanged += (_, _) => reached++;

        await evented.Service.AddSeriesLink(SeriesRequest("1"), cancellationToken);
        await evented.Service.AddSeriesLink(SeriesRequest("2"), cancellationToken);

        Assert.Single(evented.Raised);
        Assert.Equal(1, reached);
        Assert.Equal(["1", "2"], evented.Links.Series.GetAll().Select(x => x.ProviderID));
    }

    #endregion
}

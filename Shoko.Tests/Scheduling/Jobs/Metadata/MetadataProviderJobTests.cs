using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Events;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Image.Options;
using Shoko.Abstractions.Metadata.Matching;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Search;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Workers;
using Shoko.Server.Actions;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Acquisition.Filters;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Scheduling.Jobs.Metadata;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Scheduling.Jobs.Metadata;

/// <summary>
/// Covers the core's provider jobs: which job types a provider gets, what the
/// refresh, search, image and purge jobs do, and how the scheduler and the
/// library actions route requests to them, TMDB's included.
/// </summary>
public class MetadataProviderJobTests
{
    #region Fixtures

    private static readonly MetadataSource Source = TestSources.Plugin;

    private const int AnimeID = 100;

    private static MetadataGuid ID(MetadataEntityType entityType, string id)
        => new(Source, entityType, id);

    /// <summary>
    /// A provider that serves every shape, records what it was asked to
    /// refresh and writes nothing, as the core never writes for it.
    /// </summary>
    public sealed class FakeProvider : IMetadataSeriesProvider, IMetadataMovieProvider, IMetadataCollectionProvider, IMetadataAutoLinkingProvider,
        IMetadataImageProvider
    {
        public string Name => "Fake";

        public MetadataSource Source => TestSources.Plugin;

        public int? MaxConcurrentJobs => 2;

        public bool IsConfigured { get; set; } = true;

        public HashSet<MetadataGuid> Failing { get; } = [];

        public List<MetadataGuid> Refreshed { get; } = [];

        public Dictionary<MetadataGuid, MetadataRefreshOptions> Options { get; } = [];

        /// <summary>
        /// Runs inside each refresh, before it finishes.
        /// </summary>
        public Func<MetadataGuid, Task>? During { get; set; }

        public List<MetadataGuid> CleanedUp { get; } = [];

        public Dictionary<MetadataGuid, IReadOnlyList<ImageCandidate>?> Images { get; } = [];

        public List<MetadataGuid> ImagesAsked { get; } = [];

        public Task<IReadOnlyList<ImageCandidate>?> GetImages(MetadataGuid entityID, CancellationToken cancellationToken = default)
        {
            ImagesAsked.Add(entityID);
            if (Failing.Contains(entityID))
                throw new InvalidOperationException($"{entityID} failed.");

            return Task.FromResult(Images.GetValueOrDefault(entityID));
        }

        public Task RefreshSeries(MetadataGuid seriesID, MetadataRefreshOptions options, CancellationToken cancellationToken = default)
            => Refresh(seriesID, options);

        public Task RefreshMovie(MetadataGuid movieID, MetadataRefreshOptions options, CancellationToken cancellationToken = default)
            => Refresh(movieID, options);

        public Task RefreshCollection(MetadataGuid collectionID, MetadataRefreshOptions options, CancellationToken cancellationToken = default)
            => Refresh(collectionID, options);

        public Task<IReadOnlyList<MetadataAutoLinkCandidate>> FindAutoLinks(int anidbAnimeID, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<MetadataAutoLinkCandidate>>([]);

        public Task CleanUp(MetadataGuid entryID, CancellationToken cancellationToken = default)
        {
            CleanedUp.Add(entryID);
            return Task.CompletedTask;
        }

        private async Task Refresh(MetadataGuid id, MetadataRefreshOptions options)
        {
            lock (Refreshed)
            {
                Refreshed.Add(id);
                Options[id] = options;
            }

            if (During is not null)
                await During(id);
            if (Failing.Contains(id))
                throw new InvalidOperationException($"{id} failed.");
        }
    }

    /// <summary>
    /// A provider that only refreshes series, and never auto-links.
    /// </summary>
    public sealed class SeriesOnlyProvider : IMetadataSeriesProvider
    {
        public string Name => "Series only";

        public MetadataSource Source => TestSources.Plugin;

        public Task RefreshSeries(MetadataGuid seriesID, MetadataRefreshOptions options, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    /// <summary>
    /// A series provider that can be paused.
    /// </summary>
    public sealed class PausableProvider : IMetadataSeriesProvider, IPausableMetadataProvider
    {
        public string Name => "Pausable";

        public MetadataSource Source => TestSources.Plugin;

        public MetadataProviderPauseStatus PauseStatus { get; set; } = MetadataProviderPauseStatus.NotPaused;

        public event EventHandler? PauseStatusChanged;

        public void Change() => PauseStatusChanged?.Invoke(this, EventArgs.Empty);

        public Task RefreshSeries(MetadataGuid seriesID, MetadataRefreshOptions options, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    /// <summary>
    /// A provider for TMDB, the source the core keeps in tables of its own,
    /// which runs through the same jobs as any other.
    /// </summary>
    public sealed class FakeTmdbProvider : IMetadataSeriesProvider, IMetadataMovieProvider, IMetadataAutoLinkingProvider, ICoreMetadataOrphanPurger
    {
        public string Name => "TMDB";

        public MetadataSource Source => MetadataSource.TMDB;

        public List<MetadataGuid> CleanedUp { get; } = [];

        public List<DateTime> PeoplePurgedBefore { get; } = [];

        public int PeoplePurged { get; set; }

        public Task<int> PurgeOrphaned(DateTime orphanedBefore, CancellationToken cancellationToken = default)
        {
            PeoplePurgedBefore.Add(orphanedBefore);
            return Task.FromResult(PeoplePurged);
        }

        public Task RefreshSeries(MetadataGuid seriesID, MetadataRefreshOptions options, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RefreshMovie(MetadataGuid movieID, MetadataRefreshOptions options, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<MetadataAutoLinkCandidate>> FindAutoLinks(int anidbAnimeID, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<MetadataAutoLinkCandidate>>([]);

        public Task CleanUp(MetadataGuid entryID, CancellationToken cancellationToken = default)
        {
            CleanedUp.Add(entryID);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Keeps refresh times in a dictionary.
    /// </summary>
    public sealed class FakeRefreshState : IMetadataRefreshState
    {
        public Dictionary<MetadataGuid, DateTime> Times { get; } = [];

        public DateTime? GetLastRefreshedAt(MetadataGuid entry)
            => Times.TryGetValue(entry, out var time) ? time : null;

        public void RecordRefresh(MetadataGuid entry, DateTime refreshedAt)
            => Times[entry] = refreshedAt;

        public bool Forget(MetadataGuid entry)
            => Times.Remove(entry);
    }

    private static MetadataProviderInfo Info(
        IMetadataProvider provider,
        bool enabled = true,
        MetadataEntityType[]? entityTypes = null,
        bool autoLinker = true,
        bool autoLink = true,
        MetadataSource? source = null
    )
    {
        source ??= provider.Source;
        return new()
        {
            ID = Guid.NewGuid(),
            Version = new(1, 0),
            Name = provider.Name,
            Description = string.Empty,
            Provider = provider,
            ConfigurationInfo = null,
            PluginInfo = null!,
            SupportsSeries = true,
            SupportsMovies = true,
            SupportsCollections = true,
            SupportsAutoLinking = true,
            Source = source,
            MaxConcurrentJobs = provider.MaxConcurrentJobs,
            AvailableEntityTypes = new HashSet<MetadataEntityType>(),
            EnabledEntityTypes = enabled
                ? new HashSet<MetadataEntityType>(entityTypes ?? [MetadataEntityType.Series, MetadataEntityType.Movie, MetadataEntityType.Collection])
                : new HashSet<MetadataEntityType>(),
            IsAutoLinker = autoLinker,
            AutoLink = autoLinker && autoLink,
        };
    }

    private static Mock<IMetadataProviderManager> Manager(params MetadataProviderInfo[] infos)
    {
        var manager = new Mock<IMetadataProviderManager>();
        manager.SetupGet(m => m.MetadataProviders).Returns(infos);
        manager.Setup(m => m.GetProviderInfo(It.IsAny<Type>()))
            .Returns((Type type) => infos.FirstOrDefault(info => info.Provider.GetType() == type) ?? throw new ArgumentException("Unregistered.", "providerType"));
        return manager;
    }

    private static Mock<IMetadataCrossReferenceStore> Links(string[]? series = null, string[]? movies = null, MetadataEntityType? seriesKind = null)
    {
        var store = new Mock<IMetadataCrossReferenceStore>();
        var seriesLinks = (series ?? [])
            .Select(id => new CrossRef_AniDB_Metadata_Series { Source = Source, AnidbAnimeID = AnimeID, ProviderID = id, ProviderType = seriesKind ?? MetadataEntityType.Series })
            .ToList();
        var movieLinks = (movies ?? [])
            .Select(id => new CrossRef_AniDB_Metadata_Movie { Source = Source, AnidbAnimeID = AnimeID, AnidbEpisodeID = 1, ProviderID = id })
            .ToList();
        store.Setup(s => s.GetSeriesLinks(AnimeID, Source)).Returns([.. seriesLinks]);
        store.Setup(s => s.GetMovieLinksForSeries(AnimeID, Source)).Returns([.. movieLinks]);
        store.Setup(s => s.GetSeriesLinks(It.Is<int>(id => id != AnimeID), It.IsAny<MetadataSource?>())).Returns([]);
        store.Setup(s => s.GetMovieLinksForSeries(It.Is<int>(id => id != AnimeID), It.IsAny<MetadataSource?>())).Returns([]);
        store.Setup(s => s.GetSeriesLinks(AnimeID, It.Is<MetadataSource?>(source => source != Source))).Returns([]);
        store.Setup(s => s.GetMovieLinksForSeries(AnimeID, It.Is<MetadataSource?>(source => source != Source))).Returns([]);
        store.Setup(s => s.GetLinksTo(It.IsAny<MetadataGuid>())).Returns([]);
        store.Setup(s => s.GetEpisodeLinksInto(It.IsAny<MetadataGuid>())).Returns([]);
        store.Setup(s => s.GetEpisodeLinksForSeries(It.IsAny<int>(), It.IsAny<MetadataSource?>())).Returns([]);
        store.Setup(s => s.GetAllEpisodeLinks(It.IsAny<MetadataSource?>())).Returns([]);
        foreach (var link in seriesLinks)
            store.Setup(s => s.GetLinksTo(((IMetadataCrossReference)link).ProviderID!)).Returns([link]);
        foreach (var link in movieLinks)
            store.Setup(s => s.GetLinksTo(((IMetadataCrossReference)link).ProviderID!)).Returns([link]);
        return store;
    }

    private static IJobCancellationAccessor NoCancellation()
    {
        var accessor = new Mock<IJobCancellationAccessor>();
        accessor.SetupGet(a => a.Token).Returns(CancellationToken.None);
        return accessor.Object;
    }

    private static T Ready<T>(T job) where T : IQueueJob
    {
        job.Setup(new ServiceCollection().AddLogging().BuildServiceProvider());
        return job;
    }

    private sealed class RefreshHarness
    {
        public FakeProvider Provider { get; } = new();

        public SchedulerHarness Queue { get; } = new();

        public ServerSettings Settings { get; } = new();

        public Mock<IMetadataCollectionStore> CollectionStore { get; } = new();

        public Mock<IMetadataService> Metadata { get; } = new();

        public FakeRefreshState RefreshState { get; } = new();

        public MetadataEntryLocks Locks { get; } = new();

        public Mock<IMetadataImageContributorManager> Contributors { get; } = new();

        public List<(Type JobType, string EntryID)> ContributorJobs { get; } = [];

        public RefreshHarness()
        {
            Contributors.SetupGet(m => m.ImageContributors).Returns([]);
            CollectionStore.Setup(s => s.GetCollectionsWith(It.IsAny<MetadataGuid>())).Returns([]);
            Metadata.Setup(m => m.GetCollection(It.IsAny<MetadataGuid>())).Returns((MetadataGuid id) => CollectionStore.Object.GetCollection(id));
        }

        public RefreshMetadataJob<FakeProvider> Job(MetadataProviderInfo info, Mock<IMetadataCrossReferenceStore> links)
            => Job<FakeProvider>(info, links);

        public RefreshMetadataJob<TProvider> Job<TProvider>(MetadataProviderInfo info, Mock<IMetadataCrossReferenceStore> links)
            where TProvider : class, IMetadataProvider
            => Ready(new RefreshMetadataJob<TProvider>(
                Manager(info).Object,
                links.Object,
                CollectionStore.Object,
                Metadata.Object,
                RefreshState,
                Locks,
                Queue.Build(links, info),
                ContributorScheduler(Contributors, ContributorJobs),
                new StubSettingsProvider(Settings),
                NoCancellation()
            ));
    }

    #endregion

    #region Job types

    [Fact]
    public void APluginProviderGetsARefreshASearchAndAnImagesJob()
    {
        var jobTypes = MetadataProviderJobs.GetJobTypes(typeof(FakeProvider));

        Assert.Equal(
            new HashSet<Type> { typeof(RefreshMetadataJob<FakeProvider>), typeof(SearchMetadataJob<FakeProvider>), typeof(DownloadMetadataImagesJob<FakeProvider>) },
            jobTypes.ToHashSet()
        );
        Assert.Equal(typeof(FakeProvider), MetadataProviderJobs.GetProviderType(typeof(RefreshMetadataJob<FakeProvider>)));
        Assert.Equal(typeof(FakeProvider), MetadataProviderJobs.GetProviderType(typeof(SearchMetadataJob<FakeProvider>)));
        Assert.Equal(typeof(FakeProvider), MetadataProviderJobs.GetProviderType(typeof(DownloadMetadataImagesJob<FakeProvider>)));
        Assert.Equal(typeof(DownloadMetadataImagesJob<FakeProvider>), MetadataProviderJobs.GetImagesJobType(typeof(FakeProvider)));
        Assert.Null(MetadataProviderJobs.GetImagesJobType(typeof(SeriesOnlyProvider)));
    }

    [Fact]
    public void AProviderThatDoesNotAutoLinkGetsNoSearchJob()
        => Assert.Equal([typeof(RefreshMetadataJob<SeriesOnlyProvider>)], MetadataProviderJobs.GetJobTypes(typeof(SeriesOnlyProvider)));

    [Fact]
    public void TmdbsProviderGetsTheSameJobsAndOtherTypesGetNone()
    {
        Assert.Equal(
            [typeof(RefreshMetadataJob<FakeTmdbProvider>), typeof(SearchMetadataJob<FakeTmdbProvider>)],
            MetadataProviderJobs.GetJobTypes(typeof(FakeTmdbProvider))
        );
        Assert.Empty(MetadataProviderJobs.GetJobTypes(typeof(string)));
        Assert.Empty(MetadataProviderJobs.GetJobTypes(typeof(IMetadataSeriesProvider)));
        Assert.Null(MetadataProviderJobs.GetProviderType(typeof(PurgeMetadataJob)));
    }

    [Fact]
    public void EachProvidersJobsFollowItsDeclaredLimit()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Manager(Info(new FakeProvider()), Info(new SeriesOnlyProvider())).Object);
        var concurrency = new MetadataProviderJobConcurrency(services.BuildServiceProvider());

        Assert.Equal(2, concurrency.GetConcurrencyLimit(typeof(RefreshMetadataJob<FakeProvider>)));
        Assert.Equal(2, concurrency.GetConcurrencyLimit(typeof(SearchMetadataJob<FakeProvider>)));
        Assert.Equal(2, concurrency.GetConcurrencyLimit(typeof(DownloadMetadataImagesJob<FakeProvider>)));
        Assert.Null(concurrency.GetConcurrencyLimit(typeof(RefreshMetadataJob<SeriesOnlyProvider>)));
        Assert.Null(concurrency.GetConcurrencyLimit(typeof(PurgeMetadataJob)));
    }

    private sealed class PauseState : IMetadataProviderPauseState
    {
        public List<Type> Paused { get; } = [];

        public event EventHandler? PausedProvidersChanged;

        public IReadOnlyList<Type> GetPausedProviderTypes() => Paused;

        public void Change() => PausedProvidersChanged?.Invoke(this, EventArgs.Empty);
    }

    [Fact]
    public void APausedProviderHoldsBackOnlyItsOwnJobs()
    {
        var state = new PauseState();
        var filter = new MetadataProviderPausedAcquisitionFilter(state, [
            typeof(RefreshMetadataJob<FakeProvider>),
            typeof(SearchMetadataJob<FakeProvider>),
            typeof(DownloadMetadataImagesJob<FakeProvider>),
            typeof(RefreshMetadataJob<SeriesOnlyProvider>),
            typeof(PurgeMetadataJob),
        ]);
        var changes = 0;
        filter.StateChanged += (_, _) => changes++;
        Assert.Empty(filter.GetTypesToExclude());

        state.Paused.Add(typeof(FakeProvider));
        state.Change();

        Assert.Equal(1, changes);
        Assert.Equal(
            [typeof(RefreshMetadataJob<FakeProvider>), typeof(SearchMetadataJob<FakeProvider>), typeof(DownloadMetadataImagesJob<FakeProvider>)],
            filter.GetTypesToExclude()
        );
        Assert.Equal(typeof(MetadataProviderJobAttribute), filter.WatchedAttributeType);

        state.Paused.Clear();
        state.Change();

        Assert.Empty(filter.GetTypesToExclude());
    }

    #endregion

    #region Refresh

    [Fact]
    public async Task ARefreshHasTheProviderRefreshEveryLinkedSeriesAndFilm()
    {
        var harness = new RefreshHarness();
        var job = harness.Job(Info(harness.Provider), Links(series: ["1"], movies: ["9"]));
        job.AnimeID = AnimeID;
        job.Reason = MetadataRefreshReason.Scheduled;

        await job.Execute();

        var series = ID(MetadataEntityType.Series, "1");
        var movie = ID(MetadataEntityType.Movie, "9");
        Assert.Equal([series, movie], harness.Provider.Refreshed);
        foreach (var options in harness.Provider.Options.Values)
        {
            Assert.False(options.QuickRefresh);
            Assert.Equal(MetadataRefreshReason.Scheduled, options.Reason);
            Assert.Null(options.LastRefreshedAt);
            Assert.Equal(AnimeID, options.AnidbAnimeID);
        }

        Assert.Equal([series, movie], harness.RefreshState.Times.Keys);
        harness.CollectionStore.Verify(s => s.SaveCollection(It.IsAny<MetadataCollectionData>()), Times.Never);
    }

    [Fact]
    public async Task AFilmClaimingTheWholeAnimeIsRefreshedAsAFilm()
    {
        var harness = new RefreshHarness();
        var job = harness.Job(Info(harness.Provider), Links(series: ["9"], seriesKind: MetadataEntityType.Movie));
        job.AnimeID = AnimeID;

        await job.Execute();

        Assert.Equal([ID(MetadataEntityType.Movie, "9")], harness.Provider.Refreshed);
    }

    [Fact]
    public async Task TheSeriesAnEpisodeLinkPointsIntoIsRefreshedAndKeptLinked()
    {
        var harness = new RefreshHarness();
        var series = ID(MetadataEntityType.Series, "7");
        var link = new CrossRef_AniDB_Metadata_Episode { Source = Source, AnidbAnimeID = AnimeID, AnidbEpisodeID = 1, ProviderID = "71", ProviderParentID = "7" };
        var links = Links();
        links.Setup(s => s.GetEpisodeLinksForSeries(AnimeID, Source)).Returns([link]);
        links.Setup(s => s.GetEpisodeLinksInto(series)).Returns([link]);
        var job = harness.Job(Info(harness.Provider), links);
        job.AnimeID = AnimeID;

        await job.Execute();

        Assert.Equal([series], harness.Provider.Refreshed);
    }

    [Fact]
    public async Task ADisabledProviderIsNotAsked()
    {
        var harness = new RefreshHarness();
        var job = harness.Job(Info(harness.Provider, enabled: false), Links(series: ["1"]));
        job.AnimeID = AnimeID;

        await job.Execute();

        Assert.Empty(harness.Provider.Refreshed);
    }

    [Fact]
    public async Task OnlyTheEntityTypesTurnedOnAreRefreshed()
    {
        var harness = new RefreshHarness();
        var job = harness.Job(Info(harness.Provider, entityTypes: [MetadataEntityType.Movie]), Links(series: ["1"], movies: ["9"]));
        job.AnimeID = AnimeID;

        await job.Execute();

        Assert.Equal([ID(MetadataEntityType.Movie, "9")], harness.Provider.Refreshed);
    }

    [Theory]
    [InlineData("episode")]
    [InlineData("season")]
    public async Task AProviderOnForEpisodesOrSeasonsAloneRefreshesTheSeries(string entityType)
    {
        var harness = new RefreshHarness();
        var job = harness.Job(Info(harness.Provider, entityTypes: [MetadataEntityType.Parse(entityType)]), Links(series: ["1"], movies: ["9"]));
        job.AnimeID = AnimeID;

        await job.Execute();

        Assert.Equal([ID(MetadataEntityType.Series, "1")], harness.Provider.Refreshed);
    }

    [Fact]
    public async Task OneEntryIsRefreshedWhenNamed()
    {
        var harness = new RefreshHarness();
        var series = ID(MetadataEntityType.Series, "5");
        var job = harness.Job(Info(harness.Provider), Links(series: ["1", "5"]));
        job.AnimeID = AnimeID;
        job.EntryID = series.ToString();
        job.Reason = MetadataRefreshReason.Linked;

        await job.Execute();

        Assert.Equal([series], harness.Provider.Refreshed);
        Assert.Equal(MetadataRefreshReason.Linked, harness.Provider.Options[series].Reason);
        Assert.Equal(AnimeID, harness.Provider.Options[series].AnidbAnimeID);
    }

    [Fact]
    public async Task ANamedEntryNoLongerLinkedIsNotRefreshed()
    {
        var harness = new RefreshHarness();
        var info = Info(harness.Provider);
        var job = harness.Job(info, Links(series: ["1"]));
        job.EntryID = ID(MetadataEntityType.Series, "5").ToString();

        // Forced and requested, but not asked for by name.
        var requested = harness.Job(info, Links(series: ["1"]));
        requested.AnimeID = AnimeID;
        requested.EntryID = job.EntryID;
        requested.Reason = MetadataRefreshReason.Requested;
        requested.Force = true;

        await job.Execute();
        await requested.Execute();

        Assert.Empty(harness.Provider.Refreshed);
        Assert.Empty(harness.RefreshState.Times);
    }

    [Fact]
    public async Task ANamedCollectionIsRefreshedOnlyWhileItIsStored()
    {
        var harness = new RefreshHarness();
        var stored = ID(MetadataEntityType.Collection, "c");
        var gone = ID(MetadataEntityType.Collection, "gone");
        harness.CollectionStore.Setup(s => s.GetCollection(stored)).Returns(Mock.Of<ICollection>(collection => collection.ID == stored));
        var info = Info(harness.Provider);
        var storedJob = harness.Job(info, Links());
        storedJob.EntryID = stored.ToString();
        var goneJob = harness.Job(info, Links());
        goneJob.EntryID = gone.ToString();

        await storedJob.Execute();
        await goneJob.Execute();

        Assert.Equal([stored], harness.Provider.Refreshed);
        Assert.Null(harness.Provider.Options[stored].AnidbAnimeID);
    }

    [Fact]
    public async Task AnEntryOnAnotherSourceIsNotRefreshed()
    {
        var harness = new RefreshHarness();
        var job = harness.Job(Info(harness.Provider), Links());
        job.EntryID = new MetadataGuid(TestSources.AniList, MetadataEntityType.Series, "5").ToString();

        await job.Execute();

        Assert.Empty(harness.Provider.Refreshed);
    }

    [Fact]
    public async Task AnEntryRefreshedWithinTheHourIsSkippedUnlessForced()
    {
        var harness = new RefreshHarness();
        var fresh = ID(MetadataEntityType.Series, "1");
        var stale = ID(MetadataEntityType.Series, "2");
        var freshAt = DateTime.Now.AddMinutes(-10);
        var staleAt = DateTime.Now.AddHours(-2);
        harness.RefreshState.Times[fresh] = freshAt;
        harness.RefreshState.Times[stale] = staleAt;
        var info = Info(harness.Provider);
        var scheduled = harness.Job(info, Links(series: ["1", "2"]));
        scheduled.AnimeID = AnimeID;

        await scheduled.Execute();

        Assert.Equal([stale], harness.Provider.Refreshed);
        Assert.Equal(staleAt, harness.Provider.Options[stale].LastRefreshedAt);
        Assert.Equal(freshAt, harness.RefreshState.Times[fresh]);
        Assert.True(harness.RefreshState.Times[stale] > staleAt);

        var forced = harness.Job(info, Links(series: ["1", "2"]));
        forced.AnimeID = AnimeID;
        forced.Force = true;

        await forced.Execute();

        Assert.Equal([stale, fresh, stale], harness.Provider.Refreshed);
        Assert.Null(harness.Provider.Options[fresh].LastRefreshedAt);
        Assert.True(harness.RefreshState.Times[fresh] > freshAt);
    }

    [Fact]
    public async Task ANamedEntrySomebodyAskedForIsRefreshedWhileNothingLinksToIt()
    {
        var harness = new RefreshHarness();
        var series = ID(MetadataEntityType.Series, "5");
        var info = Info(harness.Provider);
        var requested = harness.Job(info, Links());
        requested.EntryID = series.ToString();
        requested.Reason = MetadataRefreshReason.Requested;
        requested.AllowUnlinked = true;
        var linked = harness.Job(info, Links());
        linked.EntryID = series.ToString();
        linked.Reason = MetadataRefreshReason.Linked;
        linked.Force = true;

        await requested.Execute();
        await linked.Execute();

        Assert.Equal([series], harness.Provider.Refreshed);
        Assert.False(harness.Locks.IsUpdating(series));
    }

    [Fact]
    public async Task AReaderCanWaitOutARefreshButNotTheImages()
    {
        var harness = new RefreshHarness();
        var series = ID(MetadataEntityType.Series, "1");
        var gate = new TaskCompletionSource();
        var started = new TaskCompletionSource();
        harness.Provider.During = async _ =>
        {
            started.TrySetResult();
            await gate.Task;
        };
        var job = harness.Job(Info(harness.Provider), Links(series: ["1"]));
        job.AnimeID = AnimeID;

        Assert.False(await harness.Locks.WaitForUpdate(series, TestContext.Current.CancellationToken));
        var run = Task.Run(job.Execute, TestContext.Current.CancellationToken);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.True(harness.Locks.IsUpdating(series));
        var waiting = harness.Locks.WaitForUpdate(series, TestContext.Current.CancellationToken);
        Assert.False(waiting.IsCompleted);

        gate.SetResult();
        Assert.True(await waiting);
        await run;
        Assert.False(harness.Locks.IsUpdating(series));

        // The image job takes the lock but is no update to wait out.
        using (await harness.Locks.Acquire(series, TestContext.Current.CancellationToken))
            Assert.False(harness.Locks.IsUpdating(series));
    }

    [Fact]
    public async Task OneFailingEntryDoesNotStopTheRestButFailsTheJob()
    {
        var harness = new RefreshHarness();
        var series = ID(MetadataEntityType.Series, "1");
        var movie = ID(MetadataEntityType.Movie, "9");
        harness.Provider.Failing.Add(series);
        var job = harness.Job(Info(harness.Provider), Links(series: ["1"], movies: ["9"]));
        job.AnimeID = AnimeID;
        job.DownloadImages = true;

        await Assert.ThrowsAsync<InvalidOperationException>(job.Execute);

        Assert.Equal([series, movie], harness.Provider.Refreshed);
        Assert.Equal([movie], harness.RefreshState.Times.Keys);
        var images = Assert.Single(harness.Queue.Queued, queued => queued.JobType == typeof(DownloadMetadataImagesJob<FakeProvider>));
        Assert.Equal(movie.ToString(), ((IMetadataImagesJob)images.Job).EntryID);
        Assert.DoesNotContain(harness.Queue.Queued, queued => queued.JobType == typeof(SyncEpisodeLinksJob));
    }

    [Fact]
    public async Task ARefreshQueuesTheLinkSyncAndTheImagesOfWhatItRefreshed()
    {
        var harness = new RefreshHarness();
        var series = ID(MetadataEntityType.Series, "1");
        var movie = ID(MetadataEntityType.Movie, "9");
        var collection = ID(MetadataEntityType.Collection, "c");
        var foreign = new MetadataGuid(TestSources.AniList, MetadataEntityType.Collection, "x");
        harness.CollectionStore.Setup(s => s.GetCollectionsWith(series)).Returns([
            Mock.Of<ICollection>(c => c.ID == collection),
            Mock.Of<ICollection>(c => c.ID == foreign),
        ]);
        var job = harness.Job(Info(harness.Provider), Links(series: ["1"], movies: ["9"]));
        job.AnimeID = AnimeID;
        job.DownloadImages = true;
        job.DownloadCrewAndCast = false;
        job.DownloadNetworks = true;

        await job.Execute();

        Assert.All(harness.Provider.Options.Values, options =>
        {
            Assert.True(options.DownloadImages);
            Assert.False(options.DownloadCrewAndCast);
            Assert.True(options.DownloadNetworks);
            Assert.Null(options.DownloadAlternateOrdering);
            Assert.Null(options.DownloadCollections);
        });
        var sync = (SyncEpisodeLinksJob)Assert.Single(harness.Queue.Queued, queued => queued.JobType == typeof(SyncEpisodeLinksJob)).Job;
        Assert.Equal(Source.Value, sync.Source);
        Assert.Equal("1", sync.SeriesID);
        var images = harness.Queue.Queued.Where(queued => queued.JobType == typeof(DownloadMetadataImagesJob<FakeProvider>)).ToList();
        Assert.Equal([series.ToString(), movie.ToString(), collection.ToString()], images.Select(queued => ((IMetadataImagesJob)queued.Job).EntryID));
        Assert.All(images, queued => Assert.False(((IMetadataImagesJob)queued.Job).Force));
        Assert.All(images, queued => Assert.False(queued.Prioritized));
    }

    [Fact]
    public async Task AQuickRefreshImagesTurnedOffOrNotAskedForQueueNoImages()
    {
        var quick = new RefreshHarness();
        var off = new RefreshHarness();
        var notAsked = new RefreshHarness();
        off.Settings.Image.MetadataSourceDefaults = NoImages();
        var quickJob = quick.Job(Info(quick.Provider), Links(series: ["1"]));
        quickJob.AnimeID = AnimeID;
        quickJob.QuickRefresh = true;
        quickJob.DownloadImages = true;
        var offJob = off.Job(Info(off.Provider), Links(series: ["1"]));
        offJob.AnimeID = AnimeID;
        offJob.DownloadImages = true;
        var notAskedJob = notAsked.Job(Info(notAsked.Provider), Links(series: ["1"]));
        notAskedJob.AnimeID = AnimeID;

        await quickJob.Execute();
        await offJob.Execute();
        await notAskedJob.Execute();

        Assert.True(quick.Provider.Options[ID(MetadataEntityType.Series, "1")].QuickRefresh);
        Assert.False(notAsked.Provider.Options[ID(MetadataEntityType.Series, "1")].DownloadImages);
        foreach (var harness in new[] { quick, off, notAsked })
        {
            Assert.Equal([ID(MetadataEntityType.Series, "1")], harness.Provider.Refreshed);
            Assert.Equal([typeof(SyncEpisodeLinksJob)], harness.Queue.Queued.Select(queued => queued.JobType));
        }
    }

    [Fact]
    public async Task ARefreshQueuesTheContributorsWhenTheOwnersImageJobDoesNotRun()
    {
        var off = new RefreshHarness();
        var noImages = new RefreshHarness();
        var series = ID(MetadataEntityType.Series, "1");
        off.Settings.Image.MetadataSourceDefaults = NoImages();
        foreach (var harness in new[] { off, noImages })
            harness.Contributors.SetupGet(m => m.ImageContributors).Returns([ContributorInfo(new ImageOnlyContributor())]);
        var offJob = off.Job(Info(off.Provider), Links(series: ["1"]));
        offJob.AnimeID = AnimeID;
        offJob.DownloadImages = true;
        var noImagesJob = noImages.Job<SeriesOnlyProvider>(Info(new SeriesOnlyProvider()), Links(series: ["1"]));
        noImagesJob.AnimeID = AnimeID;
        noImagesJob.DownloadImages = true;

        await offJob.Execute();
        await noImagesJob.Execute();

        foreach (var harness in new[] { off, noImages })
        {
            Assert.DoesNotContain(harness.Queue.Queued, queued => queued.JobType == typeof(DownloadMetadataImagesJob<FakeProvider>));
            Assert.Equal([(typeof(DownloadContributedImagesJob<ImageOnlyContributor>), series.ToString())], harness.ContributorJobs);
        }
    }

    [Fact]
    public async Task AQuickRefreshIsNotCountedAsARefresh()
    {
        var harness = new RefreshHarness();
        var series = ID(MetadataEntityType.Series, "1");
        var info = Info(harness.Provider);
        var quick = harness.Job(info, Links(series: ["1"]));
        quick.AnimeID = AnimeID;
        quick.QuickRefresh = true;
        var full = harness.Job(info, Links(series: ["1"]));
        full.AnimeID = AnimeID;

        await quick.Execute();

        Assert.False(harness.RefreshState.Times.ContainsKey(series));

        await full.Execute();

        Assert.Equal([series, series], harness.Provider.Refreshed);
        Assert.Null(harness.Provider.Options[series].LastRefreshedAt);
        Assert.True(harness.RefreshState.Times.ContainsKey(series));
    }

    [Fact]
    public async Task ARefreshWaitingOnAPurgeSkipsTheEntryItPurged()
    {
        var harness = new RefreshHarness();
        var series = ID(MetadataEntityType.Series, "1");
        var links = Links(series: ["1"]);
        var job = harness.Job(Info(harness.Provider), links);
        job.EntryID = series.ToString();
        var purging = await harness.Locks.Acquire(series, TestContext.Current.CancellationToken);

        var run = job.Execute();

        // The purge ran because the link was removed.
        links.Setup(s => s.GetLinksTo(series)).Returns([]);
        purging.Dispose();
        await run;

        Assert.Empty(harness.Provider.Refreshed);
        Assert.False(harness.Locks.IsInUse(series));
    }

    #endregion

    #region Images

    private static MetadataSourceImageSettings NoImages()
        => new()
        {
            Source = Source,
            AutoDownloadBackdrops = false,
            AutoDownloadLogos = false,
            AutoDownloadPosters = false,
            AutoDownloadBanners = false,
            AutoDownloadStaffImages = false,
            AutoDownloadStudioImages = false,
            AutoDownloadThumbnails = false,
        };

    private sealed class ImagesHarness
    {
        public FakeProvider Provider { get; } = new();

        public Mock<IMetadataService> Metadata { get; } = new();

        public Mock<IImageManager> Images { get; } = new();

        public ServerSettings Settings { get; } = new();

        public MetadataEntryLocks Locks { get; } = new();

        public Mock<IMetadataImageContributorManager> Contributors { get; } = new();

        public List<(Type JobType, string EntryID)> ContributorJobs { get; } = [];

        public ImagesHarness()
        {
            Contributors.SetupGet(m => m.ImageContributors).Returns([]);
            Images.Setup(i => i.GetTemplateUrlForSource(It.IsAny<MetadataSource>())).Returns("https://example.com/{0}");
            Images.Setup(i => i.GetImageCrossReferencesForEntity(It.IsAny<IWithImages>(), It.IsAny<ImageCrossReferenceFilteringOptions?>())).Returns([]);
        }

        public DownloadMetadataImagesJob<FakeProvider> Job(string entryID, MetadataProviderInfo? info = null)
        {
            var job = Ready(new DownloadMetadataImagesJob<FakeProvider>(
                Manager(info ?? Info(Provider)).Object,
                Metadata.Object,
                new MetadataImageReconciler(Images.Object, Locks, NullLogger<MetadataImageReconciler>.Instance),
                Locks,
                new StubSettingsProvider(Settings),
                ContributorScheduler(Contributors, ContributorJobs),
                NoCancellation()
            ));
            job.EntryID = entryID;
            return job;
        }
    }

    /// <summary>
    /// Builds a contributor scheduler whose queued jobs are recorded.
    /// </summary>
    /// <param name="contributors">The registered contributors.</param>
    /// <param name="jobs">Where each queued job's type and entry are recorded.</param>
    /// <returns>The scheduler.</returns>
    private static MetadataImageContributorScheduler ContributorScheduler(Mock<IMetadataImageContributorManager> contributors, List<(Type JobType, string EntryID)> jobs)
    {
        var queue = new Mock<IQueueScheduler>();
        queue.Setup(q => q.Enqueue(It.IsAny<Type>(), It.IsAny<Action<IQueueJob>?>(), It.IsAny<bool>()))
            .Returns((Type type, Action<IQueueJob>? configure, bool _) =>
            {
                var job = (IQueueJob)RuntimeHelpers.GetUninitializedObject(type);
                configure?.Invoke(job);
                jobs.Add((type, ((IContributedImagesJob)job).EntryID));
                return Task.CompletedTask;
            });
        return new(contributors.Object, queue.Object, NullLogger<MetadataImageContributorScheduler>.Instance);
    }

    /// <summary>
    /// A registration of a contributor with every pair of its scope on.
    /// </summary>
    /// <param name="contributor">The contributor.</param>
    /// <returns>The registration.</returns>
    private static MetadataImageContributorInfo ContributorInfo(IMetadataImageContributor contributor)
        => new()
        {
            ID = Guid.NewGuid(),
            Version = new(1, 0),
            Name = contributor.Name,
            Description = string.Empty,
            Contributor = contributor,
            PluginInfo = null!,
            Source = contributor.Source,
            MaxConcurrentJobs = 2,
            AvailableScope = contributor.Scope,
            EnabledScope = contributor.Scope,
        };

    /// <summary>
    /// A contributor adding images to the test source's series.
    /// </summary>
    public sealed class ImageOnlyContributor : IMetadataImageContributor
    {
        public string Name => "Image only";

        public MetadataSource Source => TestSources.AniList;

        public MetadataEntityScope Scope => MetadataEntityScope.Single(TestSources.Plugin, MetadataEntityType.Series);

        public Task<IReadOnlyList<ImageCandidate>?> GetImages(IMetadata entity, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ImageCandidate>?>(null);
    }

    [Fact]
    public async Task TheImageJobQueuesTheContributorsEnabledForTheEntryOnceItIsDone()
    {
        var harness = new ImagesHarness();
        var seriesID = ID(MetadataEntityType.Series, "1");
        var series = new Mock<ISeries>();
        series.SetupGet(s => s.ID).Returns(seriesID);
        series.SetupGet(s => s.Seasons).Returns([]);
        series.SetupGet(s => s.Episodes).Returns([]);
        harness.Metadata.Setup(m => m.GetSeries(seriesID)).Returns(series.Object);
        harness.Contributors.SetupGet(m => m.ImageContributors).Returns([ContributorInfo(new ImageOnlyContributor())]);

        await harness.Job(seriesID.ToString()).Execute();
        await harness.Job(ID(MetadataEntityType.Movie, "9").ToString()).Execute();

        Assert.Equal([(typeof(DownloadContributedImagesJob<ImageOnlyContributor>), seriesID.ToString())], harness.ContributorJobs);
    }

    [Fact]
    public async Task TheImageJobQueuesTheContributorsForAStoredEntryEvenWhenTheProviderIsNotAsked()
    {
        var harness = new ImagesHarness();
        var seriesID = ID(MetadataEntityType.Series, "1");
        var series = new Mock<ISeries>();
        series.SetupGet(s => s.ID).Returns(seriesID);
        series.SetupGet(s => s.Seasons).Returns([]);
        series.SetupGet(s => s.Episodes).Returns([]);
        harness.Metadata.Setup(m => m.GetSeries(seriesID)).Returns(series.Object);
        harness.Contributors.SetupGet(m => m.ImageContributors).Returns([ContributorInfo(new ImageOnlyContributor())]);
        harness.Settings.Image.MetadataSources.Add(NoImages());

        // No image type downloaded for the owner's source.
        await harness.Job(seriesID.ToString()).Execute();

        // The provider is disabled, or off for series.
        harness.Settings.Image.MetadataSources.Clear();
        await harness.Job(seriesID.ToString(), Info(harness.Provider, enabled: false)).Execute();
        await harness.Job(seriesID.ToString(), Info(harness.Provider, entityTypes: [MetadataEntityType.Movie])).Execute();

        // Not stored.
        await harness.Job(ID(MetadataEntityType.Series, "2").ToString(), Info(harness.Provider, enabled: false)).Execute();

        Assert.Empty(harness.Provider.ImagesAsked);
        Assert.Equal(3, harness.ContributorJobs.Count);
        Assert.All(harness.ContributorJobs, job => Assert.Equal((typeof(DownloadContributedImagesJob<ImageOnlyContributor>), seriesID.ToString()), job));
    }

    private static T Entity<T>(MetadataGuid id) where T : class, IMetadata
    {
        var entity = new Mock<T>();
        entity.SetupGet(e => e.ID).Returns(id);
        return entity.Object;
    }

    [Fact]
    public async Task TheImageJobAsksForTheSeriesItsPartsAndWhoIsCreditedOnTheSource()
    {
        var harness = new ImagesHarness();
        var seriesID = ID(MetadataEntityType.Series, "1");
        var seasonID = ID(MetadataEntityType.Season, "1-1");
        var episodeID = ID(MetadataEntityType.Episode, "11");
        var creatorID = ID(MetadataEntityType.Creator, "c");
        var characterID = ID(MetadataEntityType.Character, "ch");
        var studioID = ID(MetadataEntityType.Studio, "s");
        var networkID = ID(MetadataEntityType.Network, "n");
        var foreignCreatorID = new MetadataGuid(TestSources.AniList, MetadataEntityType.Creator, "x");
        var creator = Entity<ICreator>(creatorID);
        var episode = new Mock<IEpisode>();
        episode.SetupGet(e => e.ID).Returns(episodeID);
        episode.SetupGet(e => e.Crew).Returns([
            Mock.Of<ICrew>(crew => crew.CreatorID == creatorID && crew.Creator == creator),
            Mock.Of<ICrew>(crew => crew.CreatorID == foreignCreatorID && crew.Creator == Entity<ICreator>(foreignCreatorID)),
        ]);
        var series = new Mock<ISeries>();
        series.SetupGet(s => s.ID).Returns(seriesID);
        series.SetupGet(s => s.Seasons).Returns([Entity<ISeason>(seasonID)]);
        series.SetupGet(s => s.Episodes).Returns([episode.Object]);
        series.SetupGet(s => s.Cast).Returns([
            Mock.Of<ICast>(cast => cast.CreatorID == creatorID && cast.CharacterID == characterID && cast.Creator == creator &&
                cast.Character == Entity<ICharacter>(characterID)),
        ]);
        series.SetupGet(s => s.Studios).Returns([Entity<IStudio>(studioID)]);
        series.SetupGet(s => s.Networks).Returns([Entity<INetwork>(networkID), Entity<INetwork>(new(TestSources.AniList, MetadataEntityType.Network, "x"))]);
        harness.Metadata.Setup(m => m.GetSeries(seriesID)).Returns(series.Object);
        harness.Provider.Images[seriesID] = [new() { ResourceID = "poster.jpg", ImageType = ImageEntityType.Primary }];

        await harness.Job(seriesID.ToString()).Execute();

        Assert.Equal([seriesID, seasonID, episodeID, creatorID, characterID, studioID, networkID], harness.Provider.ImagesAsked);
        harness.Images.Verify(i => i.GetImageBySourceAndRemoteResourceID(Source, "poster.jpg", false), Times.Once);
        harness.Images.Verify(i => i.ScheduleAutoDownloadsForEntity(series.Object, Source, null, Source, false), Times.Once);
    }

    [Fact]
    public async Task TheImageJobSkipsWhatItCannotOrShouldNotDo()
    {
        var harness = new ImagesHarness();
        var movieID = ID(MetadataEntityType.Movie, "9");

        // Not stored.
        await harness.Job(movieID.ToString()).Execute();

        // On another source.
        harness.Metadata.Setup(m => m.GetMovie(It.IsAny<MetadataGuid>())).Returns((MetadataGuid id) => Entity<IMovie>(id));
        await harness.Job(new MetadataGuid(TestSources.AniList, MetadataEntityType.Movie, "9").ToString()).Execute();

        // Disabled.
        await harness.Job(movieID.ToString(), Info(harness.Provider, enabled: false)).Execute();

        // No image type downloaded.
        harness.Settings.Image.MetadataSources.Add(NoImages());
        await harness.Job(movieID.ToString()).Execute();

        Assert.Empty(harness.Provider.ImagesAsked);
    }

    [Fact]
    public async Task TheImageJobAsksNothingForAKindTheProviderIsNotEnabledFor()
    {
        var harness = new ImagesHarness();
        var seriesID = ID(MetadataEntityType.Series, "1");
        var movieID = ID(MetadataEntityType.Movie, "9");
        var series = new Mock<ISeries>();
        series.SetupGet(s => s.ID).Returns(seriesID);
        series.SetupGet(s => s.Seasons).Returns([]);
        series.SetupGet(s => s.Episodes).Returns([]);
        harness.Metadata.Setup(m => m.GetSeries(seriesID)).Returns(series.Object);
        harness.Metadata.Setup(m => m.GetMovie(movieID)).Returns(Entity<IMovie>(movieID));
        var moviesOnly = Info(harness.Provider, entityTypes: [MetadataEntityType.Movie]);

        await harness.Job(seriesID.ToString(), moviesOnly).Execute();
        await harness.Job(movieID.ToString(), moviesOnly).Execute();

        Assert.Equal([movieID], harness.Provider.ImagesAsked);
    }

    [Fact]
    public async Task OneFailingEntityDoesNotStopTheRestButFailsTheImageJob()
    {
        var harness = new ImagesHarness();
        var seriesID = ID(MetadataEntityType.Series, "1");
        var episodeID = ID(MetadataEntityType.Episode, "11");
        var series = new Mock<ISeries>();
        series.SetupGet(s => s.ID).Returns(seriesID);
        series.SetupGet(s => s.Seasons).Returns([]);
        series.SetupGet(s => s.Episodes).Returns([Entity<IEpisode>(episodeID)]);
        harness.Metadata.Setup(m => m.GetSeries(seriesID)).Returns(series.Object);
        harness.Provider.Failing.Add(seriesID);

        await Assert.ThrowsAsync<InvalidOperationException>(harness.Job(seriesID.ToString()).Execute);

        Assert.Equal([seriesID, episodeID], harness.Provider.ImagesAsked);
    }

    [Fact]
    public async Task TheImageJobWaitsForTheEntrysLockAndSkipsAnEntryPurgedMeanwhile()
    {
        var harness = new ImagesHarness();
        var movieID = ID(MetadataEntityType.Movie, "9");
        harness.Metadata.Setup(m => m.GetMovie(movieID)).Returns(Entity<IMovie>(movieID));

        Task running;
        using (await harness.Locks.Acquire(movieID, TestContext.Current.CancellationToken))
        {
            running = harness.Job(movieID.ToString()).Execute();

            // Purged while the job waited.
            harness.Metadata.Setup(m => m.GetMovie(movieID)).Returns((IMovie?)null);
        }

        await running;
        Assert.Empty(harness.Provider.ImagesAsked);
    }

    [Fact]
    public async Task ATmdbImageJobRunsBesideARefreshButWaitsForAPurge()
    {
        var harness = new ImagesHarness();
        var info = Info(harness.Provider, source: MetadataSource.TMDB);
        var movieID = new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Movie, "9");
        harness.Metadata.Setup(m => m.GetMovie(movieID)).Returns(Entity<IMovie>(movieID));

        // A refresh holds only the entry's lock.
        using (await harness.Locks.Acquire(movieID, TestContext.Current.CancellationToken))
            await harness.Job(movieID.ToString(), info).Execute();

        Assert.Equal([movieID], harness.Provider.ImagesAsked);

        // A purge holds the image lock too.
        Task running;
        using (await harness.Locks.AcquireImages(movieID, TestContext.Current.CancellationToken))
        {
            running = harness.Job(movieID.ToString(), info).Execute();
            Assert.Single(harness.Provider.ImagesAsked);
        }

        await running;
        Assert.Equal([movieID, movieID], harness.Provider.ImagesAsked);
    }

    [Fact]
    public async Task TheImageJobLeavesOutTheKindsNothingIsDownloadedFor()
    {
        var harness = new ImagesHarness();
        var movieID = ID(MetadataEntityType.Movie, "9");
        var creatorID = ID(MetadataEntityType.Creator, "c");
        var creator = Entity<ICreator>(creatorID);
        var movie = new Mock<IMovie>();
        movie.SetupGet(m => m.ID).Returns(movieID);
        movie.SetupGet(m => m.OriginalLanguageCode).Returns("ja");
        movie.SetupGet(m => m.Crew).Returns([Mock.Of<ICrew>(crew => crew.CreatorID == creatorID && crew.Creator == creator)]);
        harness.Metadata.Setup(m => m.GetMovie(movieID)).Returns(movie.Object);
        harness.Settings.Image.MetadataSources.Add(new() { Source = Source, AutoDownloadStaffImages = false });

        await harness.Job(movieID.ToString()).Execute();

        Assert.Equal([movieID], harness.Provider.ImagesAsked);
    }

    #endregion

    #region Search

    /// <summary>
    /// An auto-linker that links series and films, and hands back the
    /// candidates it is given.
    /// </summary>
    public sealed class FakeAutoLinker : IMetadataSeriesLinkingProvider, IMetadataMovieLinkingProvider, IMetadataAutoLinkingProvider
    {
        public string Name => "Auto-linker";

        public MetadataSource Source => TestSources.Plugin;

        public bool IsConfigured { get; set; } = true;

        public IReadOnlySet<MetadataEntityType> LinkableEntityTypes { get; } =
            new HashSet<MetadataEntityType> { MetadataEntityType.Series, MetadataEntityType.Episode };

        public List<int> AutoLinked { get; } = [];

        public List<MetadataAutoLinkCandidate> Candidates { get; } = [];

        public Exception? Failure { get; init; }

        public Task<IReadOnlyList<MetadataAutoLinkCandidate>> FindAutoLinks(int anidbAnimeID, CancellationToken cancellationToken = default)
        {
            AutoLinked.Add(anidbAnimeID);
            if (Failure is not null)
                throw Failure;

            return Task.FromResult<IReadOnlyList<MetadataAutoLinkCandidate>>([.. Candidates]);
        }

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
    }

    /// <summary>
    /// A search job over a real link store, with the linking service applying
    /// what the auto-linker takes.
    /// </summary>
    private sealed class SearchHarness
    {
        public SearchHarness(
            FakeAutoLinker? provider = null,
            bool autoLinker = true,
            bool autoLink = true,
            IShokoSeries? series = null,
            MetadataEntityType[]? entityTypes = null
        )
        {
            Provider = provider ?? new FakeAutoLinker();
            Info = Info(
                Provider,
                entityTypes: entityTypes ?? [MetadataEntityType.Series, MetadataEntityType.Episode, MetadataEntityType.Movie],
                autoLinker: autoLinker,
                autoLink: autoLink
            );
            var manager = Manager(Info);
            manager.Setup(m => m.GetAvailableProviders(It.IsAny<bool>(), It.IsAny<bool>())).Returns([Info]);
            manager.Setup(m => m.GetAvailableProviders(It.IsAny<MetadataEntityType>(), It.IsAny<MetadataSource?>()))
                .Returns((MetadataEntityType entityType, MetadataSource? source) =>
                    (source is null || source == Source) && Info.EnabledEntityTypes.Contains(entityType) ? [Info] : []);
            manager.Setup(m => m.GetProviderInfo(It.IsAny<IMetadataProvider>())).Returns(Info);
            var metadata = new Mock<IMetadataService>();
            metadata.Setup(m => m.GetShokoSeriesByAnidbID(AnimeID)).Returns(series);
            var scheduler = Queue.Build(Links.Store, Info);
            var linking = new MetadataLinkingService(
                NullLogger<MetadataLinkingService>.Instance,
                manager.Object,
                Links.Store,
                metadata.Object,
                scheduler,
                CachedRepo.Build<AnimeSeriesRepository, int, AnimeSeries>(row => row.AnimeSeriesID),
                CachedRepo.Build<AniDB_AnimeRepository, int, AniDB_Anime>(row => row.AniDB_AnimeID),
                CachedRepo.Build<AniDB_EpisodeRepository, int, AniDB_Episode>(
                    row => row.AniDB_EpisodeID,
                    new AniDB_Episode { AniDB_EpisodeID = 1, EpisodeID = 5, AnimeID = AnimeID, EpisodeType = EpisodeType.Episode }
                )
            );
            Job = Ready(new SearchMetadataJob<FakeAutoLinker>(manager.Object, Links.Store, metadata.Object, linking, scheduler, NoCancellation()));
            Job.AnimeID = AnimeID;
        }

        public FakeAutoLinker Provider { get; }

        public MetadataProviderInfo Info { get; }

        public WritableLinkStore Links { get; } = new();

        public SchedulerHarness Queue { get; } = new();

        public SearchMetadataJob<FakeAutoLinker> Job { get; }

        public List<string> SeriesLinks
            => [.. Links.Series.GetAll().Where(row => row.AnidbAnimeID == AnimeID).Select(row => row.ProviderID)];

        public async Task Link(string seriesID, MatchRating rating = MatchRating.UserVerified)
            => await Links.Store.MergeSeriesLinks(
                [new() { Source = Source, AnidbAnimeID = AnimeID, ProviderID = ID(MetadataEntityType.Series, seriesID), MatchRating = rating }],
                cancellationToken: TestContext.Current.CancellationToken
            );
    }

    private static MetadataAutoLinkCandidate Candidate(string id, MetadataEntityType? entityType = null, int? anidbEpisodeID = null, MatchRejectionReason? rejected = null)
    {
        var entryID = ID(entityType ?? MetadataEntityType.Series, id);
        return new()
        {
            Result = entryID.EntityType == MetadataEntityType.Movie
                ? new MetadataMovieSearchResult { ID = entryID, Title = $"Film {id}" }
                : new MetadataSeriesSearchResult { ID = entryID, Title = $"Series {id}" },
            AnidbAnimeID = AnimeID,
            AnidbEpisodeID = anidbEpisodeID,
            MatchRating = MatchRating.TitleMatches,
            IsRemote = true,
            Rejection = rejected is { } reason ? new() { Reason = reason } : null,
        };
    }

    [Fact]
    public async Task ASearchAsksTheAutoLinkerAndLinksWhatItTakes()
    {
        var search = new SearchHarness();
        search.Provider.Candidates.AddRange([Candidate("1"), Candidate("2", rejected: MatchRejectionReason.Outranked), Candidate("8", MetadataEntityType.Movie, 5)]);

        await search.Job.Execute();

        Assert.Equal([AnimeID], search.Provider.AutoLinked);
        Assert.Equal(["1"], search.SeriesLinks);
        var link = Assert.Single(search.Links.Series.GetAll());
        Assert.Equal(MatchRating.TitleMatches, link.MatchRating);
        var movie = Assert.Single(search.Links.Movies.GetAll());
        Assert.Equal(("8", 5), (movie.ProviderID, movie.AnidbEpisodeID));
    }

    [Fact]
    public async Task AnUnconfiguredAutoLinkerIsSkippedQuietly()
    {
        var search = new SearchHarness();
        search.Provider.IsConfigured = false;
        search.Provider.Candidates.Add(Candidate("1"));
        search.Job.Force = true;

        await search.Job.Execute();

        Assert.Empty(search.Provider.AutoLinked);
        Assert.Empty(search.SeriesLinks);
        Assert.Empty(search.Queue.Queued);

        search.Provider.IsConfigured = true;
        await search.Job.Execute();

        Assert.Equal(["1"], search.SeriesLinks);
    }

    [Fact]
    public async Task OnlyTheSourcesAutoLinkerIsAsked()
    {
        var search = new SearchHarness(autoLinker: false);
        search.Job.Force = true;

        await search.Job.Execute();

        Assert.Empty(search.Provider.AutoLinked);
    }

    [Fact]
    public async Task AScheduledSearchRespectsTheSettingTheVetoAndTheLinks()
    {
        var notAutoLinking = new SearchHarness(autoLink: false);
        var vetoed = new Mock<IShokoSeries>();
        vetoed.Setup(s => s.IsAutoLinkingDisabled(Source)).Returns(true);
        var withVeto = new SearchHarness(series: vetoed.Object);
        var alreadyLinked = new SearchHarness();
        await alreadyLinked.Link("1");

        await notAutoLinking.Job.Execute();
        await withVeto.Job.Execute();
        await alreadyLinked.Job.Execute();

        Assert.Empty(notAutoLinking.Provider.AutoLinked);
        Assert.Empty(withVeto.Provider.AutoLinked);
        Assert.Empty(alreadyLinked.Provider.AutoLinked);
    }

    [Fact]
    public async Task AScheduledSearchIsNotHeldBackByEpisodeLinksToNothing()
    {
        var search = new SearchHarness();
        await search.Links.Store.MergeEpisodeLinks(
            [new() { Source = Source, AnidbAnimeID = AnimeID, AnidbEpisodeID = 5, ProviderID = null, MatchRating = MatchRating.UserVerified }],
            cancellationToken: TestContext.Current.CancellationToken
        );

        await search.Job.Execute();

        Assert.Equal([AnimeID], search.Provider.AutoLinked);
    }

    [Fact]
    public async Task AForcedSearchIgnoresTheSettingAndTheVeto_ButNotTheLinks()
    {
        var vetoed = new Mock<IShokoSeries>();
        vetoed.Setup(s => s.IsAutoLinkingDisabled(Source)).Returns(true);
        var search = new SearchHarness(autoLink: false, series: vetoed.Object);
        search.Job.Force = true;
        var linked = new SearchHarness(autoLink: false, series: vetoed.Object);
        await linked.Link("1");
        linked.Job.Force = true;

        await search.Job.Execute();
        await linked.Job.Execute();

        Assert.Equal([AnimeID], search.Provider.AutoLinked);
        Assert.Empty(linked.Provider.AutoLinked);
    }

    [Fact]
    public async Task AReplacingSearchGoesAheadRegardless()
    {
        var vetoed = new Mock<IShokoSeries>();
        vetoed.Setup(s => s.IsAutoLinkingDisabled(Source)).Returns(true);
        var search = new SearchHarness(autoLink: false, series: vetoed.Object);
        await search.Link("1");
        search.Job.Replace = true;

        await search.Job.Execute();

        Assert.Equal([AnimeID], search.Provider.AutoLinked);
    }

    [Fact]
    public async Task ASearchThatLinkedQueuesARefreshOfWhatTheAnimeNowLinksTo()
    {
        var search = new SearchHarness();
        search.Provider.Candidates.Add(Candidate("1"));

        await search.Job.Execute();

        var (type, queued, _) = Assert.Single(search.Queue.Queued);
        Assert.Equal(typeof(RefreshMetadataJob<FakeAutoLinker>), type);
        var refresh = (IMetadataRefreshJob)queued;
        Assert.Equal(AnimeID, refresh.AnimeID);
        Assert.Null(refresh.EntryID);
        Assert.False(refresh.Force);
        Assert.Equal(MetadataRefreshReason.Linked, refresh.Reason);
    }

    [Fact]
    public async Task AReplacingSearchReplacesEveryLinkAndForcesTheRefresh()
    {
        var search = new SearchHarness();
        await search.Link("1");
        await search.Links.Store.MergeMovieLinks(
            [new() { Source = Source, AnidbAnimeID = AnimeID, AnidbEpisodeID = 5, ProviderID = ID(MetadataEntityType.Movie, "9"), MatchRating = MatchRating.UserVerified }],
            cancellationToken: TestContext.Current.CancellationToken
        );
        await search.Links.Store.MergeEpisodeLinks(
            [new() { Source = Source, AnidbAnimeID = AnimeID, AnidbEpisodeID = 5, ProviderID = null, MatchRating = MatchRating.UserVerified }],
            cancellationToken: TestContext.Current.CancellationToken
        );
        search.Provider.Candidates.Add(Candidate("2"));
        search.Job.Replace = true;

        await search.Job.Execute();

        Assert.Equal(["2"], search.SeriesLinks);
        Assert.Empty(search.Links.Movies.GetAll());
        Assert.Empty(search.Links.Episodes.GetAll());
        var refresh = (IMetadataRefreshJob)Assert.Single(search.Queue.Queued).Job;
        Assert.Equal(AnimeID, refresh.AnimeID);
        Assert.True(refresh.Force);
        Assert.Equal(MetadataRefreshReason.Linked, refresh.Reason);
    }

    [Fact]
    public async Task AReplacingSearchKeepsAnEntryItTookAgain_ButNotItsEpisodeLinks()
    {
        var search = new SearchHarness();
        await search.Link("1");
        await search.Links.Store.MergeEpisodeLinks(
            [
                new()
                {
                    Source = Source,
                    AnidbAnimeID = AnimeID,
                    AnidbEpisodeID = 5,
                    ProviderID = ID(MetadataEntityType.Episode, "50"),
                    ProviderParentID = ID(MetadataEntityType.Series, "1"),
                    MatchRating = MatchRating.UserVerified,
                },
            ],
            cancellationToken: TestContext.Current.CancellationToken
        );
        search.Provider.Candidates.Add(Candidate("1"));
        search.Job.Replace = true;

        await search.Job.Execute();

        var link = Assert.Single(search.Links.Series.GetAll());
        Assert.Equal(("1", MatchRating.TitleMatches), (link.ProviderID, link.MatchRating));
        Assert.Empty(search.Links.Episodes.GetAll());
    }

    // A film claiming the whole anime is written by the series provider, so
    // with series linking off it is refused before anything is removed.
    [Fact]
    public async Task AReplacingSearchLeavesEveryLinkWhenWhatItTookCannotBeWritten()
    {
        var search = new SearchHarness(entityTypes: [MetadataEntityType.Movie]);
        await search.Links.Store.MergeMovieLinks(
            [new() { Source = Source, AnidbAnimeID = AnimeID, AnidbEpisodeID = 5, ProviderID = ID(MetadataEntityType.Movie, "9"), MatchRating = MatchRating.UserVerified }],
            cancellationToken: TestContext.Current.CancellationToken
        );
        search.Provider.Candidates.Add(Candidate("8", MetadataEntityType.Movie));
        search.Job.Replace = true;

        await search.Job.Execute();

        var movie = Assert.Single(search.Links.Movies.GetAll());
        Assert.Equal("9", movie.ProviderID);
        Assert.Empty(search.Links.Series.GetAll());
        Assert.Empty(search.Queue.Queued);
    }

    [Fact]
    public async Task AReplacingSearchThatTookNothingLeavesEveryLink()
    {
        var search = new SearchHarness();
        await search.Link("1");
        search.Provider.Candidates.AddRange([Candidate("2", rejected: MatchRejectionReason.TitleMismatch), Candidate("3", MetadataEntityType.Episode)]);
        search.Job.Replace = true;

        await search.Job.Execute();

        // One the provider turned down, and one the core refuses, as an
        // episode is no whole work.
        Assert.Equal([AnimeID], search.Provider.AutoLinked);
        Assert.Equal(["1"], search.SeriesLinks);
        Assert.Empty(search.Queue.Queued);
    }

    [Fact]
    public async Task AReplacingSearchThatFailedLeavesEveryLink()
    {
        var search = new SearchHarness(new FakeAutoLinker { Failure = new MetadataProviderUnavailableException(Source, "Down.") });
        await search.Link("1");
        search.Job.Replace = true;

        await Assert.ThrowsAsync<MetadataProviderUnavailableException>(search.Job.Execute);

        Assert.Equal(["1"], search.SeriesLinks);
        Assert.Empty(search.Queue.Queued);
    }

    [Fact]
    public async Task ASearchThatFoundNothingQueuesNothing()
    {
        var search = new SearchHarness();

        await search.Job.Execute();

        Assert.Equal([AnimeID], search.Provider.AutoLinked);
        Assert.Empty(search.SeriesLinks);
        Assert.Empty(search.Queue.Queued);
    }

    #endregion

    #region Purge

    private sealed class PurgeHarness
    {
        public FakeProvider Provider { get; } = new();

        public Mock<IMetadataCrossReferenceStore> Links { get; set; } = MetadataProviderJobTests.Links();

        public Mock<IMetadataSeriesStore> SeriesStore { get; } = new();

        public Mock<IMetadataMovieStore> MovieStore { get; } = new();

        public Mock<IMetadataCollectionStore> CollectionStore { get; } = new();

        public Mock<IMetadataPeopleStore> People { get; } = new();

        public Mock<IMetadataTagStore> Tags { get; } = new();

        public Mock<IMetadataStudioStore> Studios { get; } = new();

        public Mock<IMetadataRelationStore> Relations { get; } = new();

        public Mock<IMetadataSuggestionStore> Suggestions { get; } = new();

        public Mock<IImageManager> Images { get; } = new();

        public FakeRefreshState RefreshState { get; } = new();

        public MetadataEntryLocks Locks { get; } = new();

        public SchedulerHarness Queue { get; } = new();

        public List<FakeProvider> Others { get; } = [];

        public FakeTmdbProvider Tmdb { get; } = new();

        public Mock<IMetadataService> Metadata { get; } = new();

        public OrderingTables Orderings { get; } = new();

        public MetadataLinkChangeTracker LinkChanges { get; } = new();

        public PurgeHarness()
        {
            CollectionStore.Setup(s => s.GetCollectionsWith(It.IsAny<MetadataGuid>())).Returns([]);
            CollectionStore.Setup(s => s.GetMembers(It.IsAny<MetadataGuid>())).Returns([]);
            Images.Setup(i => i.GetImageCrossReferencesForEntity(It.IsAny<IWithImages>(), It.IsAny<ImageCrossReferenceFilteringOptions?>())).Returns([]);
        }

        public PurgeMetadataJob Job(string entryID)
        {
            MetadataProviderInfo[] infos = [Info(Provider, enabled: false), .. Others.Select(other => Info(other)), Info(Tmdb)];
            var job = Ready(new PurgeMetadataJob(
                Manager(infos).Object,
                Links.Object,
                SeriesStore.Object,
                MovieStore.Object,
                CollectionStore.Object,
                Metadata.Object,
                new MetadataEntityCleanup(People.Object, Tags.Object, Studios.Object, Relations.Object, Suggestions.Object, Images.Object),
                Orderings.Build(() => Metadata.Object),
                RefreshState,
                Locks,
                Queue.Build(Links, infos),
                NoCancellation(),
                LinkChanges
            ));
            job.EntryID = entryID;
            return job;
        }
    }

    [Fact]
    public async Task APurgeRemovesTheStoredSeriesThroughItsStore()
    {
        var harness = new PurgeHarness();
        var seriesID = ID(MetadataEntityType.Series, "1");
        harness.SeriesStore.Setup(s => s.RemoveSeries(seriesID)).Returns(3);

        await harness.Job(seriesID.ToString()).Execute();

        harness.SeriesStore.Verify(s => s.RemoveSeries(seriesID), Times.Once);
        harness.Tags.Verify(s => s.RemoveTags(It.IsAny<MetadataGuid>()), Times.Never);
        Assert.Equal([seriesID], harness.Provider.CleanedUp);
    }

    [Fact]
    public async Task APurgeOfAFilmQueuesThePurgeOfTheCollectionsHoldingIt()
    {
        var harness = new PurgeHarness();
        var movieID = ID(MetadataEntityType.Movie, "9");
        var collectionID = ID(MetadataEntityType.Collection, "c");
        harness.CollectionStore.Setup(s => s.GetCollectionsWith(movieID)).Returns([Entity<ICollection>(collectionID)]);

        await harness.Job(movieID.ToString()).Execute();

        var (type, job, _) = Assert.Single(harness.Queue.Queued);
        Assert.Equal(typeof(PurgeMetadataJob), type);
        Assert.Equal(collectionID.ToString(), ((PurgeMetadataJob)job).EntryID);
    }

    [Fact]
    public async Task ACollectionIsPurgedOnlyOnceNoMemberIsLinked()
    {
        var harness = new PurgeHarness();
        harness.Links = Links(movies: ["9"]);
        var collectionID = ID(MetadataEntityType.Collection, "c");
        harness.CollectionStore.Setup(s => s.GetMembers(collectionID)).Returns([ID(MetadataEntityType.Movie, "9"), ID(MetadataEntityType.Series, "8")]);

        await harness.Job(collectionID.ToString()).Execute();

        harness.CollectionStore.Verify(s => s.RemoveCollection(It.IsAny<MetadataGuid>()), Times.Never);
        Assert.Empty(harness.Provider.CleanedUp);

        harness.Links = Links();
        await harness.Job(collectionID.ToString()).Execute();

        harness.CollectionStore.Verify(s => s.RemoveCollection(collectionID), Times.Once);
        Assert.Equal([collectionID], harness.Provider.CleanedUp);
        Assert.Empty(harness.Queue.Queued);
    }

    [Fact]
    public async Task AForcedPurgeRemovesTheLinksFirst()
    {
        var harness = new PurgeHarness();
        var seriesID = ID(MetadataEntityType.Series, "1");
        var seriesLink = new CrossRef_AniDB_Metadata_Series { Source = Source, AnidbAnimeID = AnimeID, ProviderID = "1" };
        var episodeLink = new CrossRef_AniDB_Metadata_Episode { Source = Source, AnidbAnimeID = AnimeID, AnidbEpisodeID = 1, ProviderID = "11", ProviderParentID = "1" };
        // Left by matching, and gone with the anime's last series link.
        var unmatched = new CrossRef_AniDB_Metadata_Episode { Source = Source, AnidbAnimeID = AnimeID, AnidbEpisodeID = 2, ProviderID = "", ProviderParentID = "" };
        harness.Links.Setup(s => s.GetLinksTo(seriesID)).Returns([seriesLink]);
        harness.Links.Setup(s => s.GetEpisodeLinksInto(seriesID)).Returns([episodeLink]);
        harness.Links.Setup(s => s.GetEpisodeLinksForSeries(AnimeID, Source)).Returns([episodeLink, unmatched]);
        harness.Links.Setup(s => s.MergeSeriesLinks(It.IsAny<IEnumerable<MetadataSeriesLinkData>>(), It.IsAny<IEnumerable<IMetadataSeriesCrossReference>?>(),
                It.IsAny<MetadataLinkUpdateOptions?>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                harness.Links.Setup(s => s.GetLinksTo(seriesID)).Returns([]);
                harness.Links.Setup(s => s.GetEpisodeLinksInto(seriesID)).Returns([]);
            })
            .ReturnsAsync([]);
        var job = harness.Job(seriesID.ToString());
        job.Force = true;

        await job.Execute();

        harness.Links.Verify(s => s.MergeSeriesLinks(
            It.Is<IEnumerable<MetadataSeriesLinkData>>(links => !links.Any()),
            It.Is<IEnumerable<IMetadataSeriesCrossReference>?>(removals => removals!.SequenceEqual(new[] { seriesLink })),
            null,
            It.IsAny<CancellationToken>()
        ), Times.Once);
        harness.Links.Verify(s => s.MergeEpisodeLinks(
            It.Is<IEnumerable<MetadataEpisodeLinkData>>(links => !links.Any()),
            It.Is<IEnumerable<IMetadataEpisodeCrossReference>?>(removals => removals!.SequenceEqual(new[] { episodeLink, unmatched })),
            null,
            It.IsAny<CancellationToken>()
        ), Times.Once);
        harness.SeriesStore.Verify(s => s.RemoveSeries(seriesID), Times.Once);
        Assert.Equal([seriesID], harness.Provider.CleanedUp);
    }

    // The links a forced purge removes are reported together, as purged,
    // whichever of the store's writes removed them.
    [Fact]
    public async Task TheLinksAForcedPurgeRemovesAreOneEvent()
    {
        var harness = new PurgeHarness();
        var seriesID = ID(MetadataEntityType.Series, "1");
        var seriesLink = new CrossRef_AniDB_Metadata_Series { Source = Source, AnidbAnimeID = AnimeID, ProviderID = "1", MatchRating = MatchRating.TitleMatches };
        var episodeLink = new CrossRef_AniDB_Metadata_Episode { Source = Source, AnidbAnimeID = AnimeID, AnidbEpisodeID = 1, ProviderID = "11", ProviderParentID = "1" };
        harness.Links.Setup(s => s.GetLinksTo(seriesID)).Returns([seriesLink]);
        harness.Links.Setup(s => s.GetEpisodeLinksInto(seriesID)).Returns([episodeLink]);
        harness.Links.Setup(s => s.MergeSeriesLinks(It.IsAny<IEnumerable<MetadataSeriesLinkData>>(), It.IsAny<IEnumerable<IMetadataSeriesCrossReference>?>(),
                It.IsAny<MetadataLinkUpdateOptions?>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                harness.LinkChanges.Record([MetadataLinkChangeTracker.LinkRowChange.Of(seriesLink, seriesLink.MatchRating, null)]);
                harness.Links.Setup(s => s.GetLinksTo(seriesID)).Returns([]);
                harness.Links.Setup(s => s.GetEpisodeLinksInto(seriesID)).Returns([]);
            })
            .ReturnsAsync([]);
        harness.Links.Setup(s => s.MergeEpisodeLinks(It.IsAny<IEnumerable<MetadataEpisodeLinkData>>(), It.IsAny<IEnumerable<IMetadataEpisodeCrossReference>?>(),
                It.IsAny<MetadataLinkUpdateOptions?>(), It.IsAny<CancellationToken>()))
            .Callback(() => harness.LinkChanges.Record([MetadataLinkChangeTracker.LinkRowChange.Of(episodeLink, episodeLink.MatchRating, null)]))
            .ReturnsAsync([]);
        var raised = new List<MetadataLinksChangedEventArgs>();
        harness.LinkChanges.Changed += (_, eventArgs) => raised.Add(eventArgs);
        var job = harness.Job(seriesID.ToString());
        job.Force = true;

        await job.Execute();

        var purged = Assert.Single(raised);
        Assert.Equal(MetadataLinkChangeReason.Purge, purged.Reason);
        Assert.Equal(
            [(MetadataLinkChangeKind.Removed, MetadataEntityType.Series, "1"), (MetadataLinkChangeKind.Removed, MetadataEntityType.Episode, "11")],
            purged.Changes.Select(change => (change.Kind, change.EntityType, change.ProviderID!.ID))
        );
    }

    [Fact]
    public async Task AForcedPurgeOfACollectionGoesAheadWhileAMemberIsLinked()
    {
        var harness = new PurgeHarness();
        harness.Links = Links(movies: ["9"]);
        var collectionID = ID(MetadataEntityType.Collection, "c");
        harness.CollectionStore.Setup(s => s.GetMembers(collectionID)).Returns([ID(MetadataEntityType.Movie, "9")]);
        var job = harness.Job(collectionID.ToString());
        job.Force = true;

        await job.Execute();

        harness.CollectionStore.Verify(s => s.RemoveCollection(collectionID), Times.Once);
        Assert.Equal([collectionID], harness.Provider.CleanedUp);
    }

    [Fact]
    public async Task ATmdbCollectionIsKeptWhileOneOfItsMoviesIsLinked()
    {
        var harness = new PurgeHarness();
        var collectionID = new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Collection, "5");
        var linkedMovie = new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Movie, "9");
        var collection = new Mock<Abstractions.Metadata.Tmdb.ITmdbCollection>();
        collection.SetupGet(c => c.ID).Returns(collectionID);
        collection.SetupGet(c => c.Movies).Returns([Mock.Of<Abstractions.Metadata.Tmdb.ITmdbMovie>(movie => movie.ID == linkedMovie)]);
        harness.Metadata.Setup(m => m.GetCollection(collectionID)).Returns(collection.Object);
        harness.Links.Setup(s => s.GetLinksTo(linkedMovie))
            .Returns([new CrossRef_AniDB_Metadata_Movie { Source = MetadataSource.TMDB, AnidbAnimeID = AnimeID, AnidbEpisodeID = 1, ProviderID = "9" }]);

        await harness.Job(collectionID.ToString()).Execute();

        Assert.Empty(harness.Tmdb.CleanedUp);

        harness.Links.Setup(s => s.GetLinksTo(linkedMovie)).Returns([]);
        await harness.Job(collectionID.ToString()).Execute();

        Assert.Equal([collectionID], harness.Tmdb.CleanedUp);
    }

    [Fact]
    public async Task APurgeOfAFilmNothingStoresStillClearsItsData()
    {
        var harness = new PurgeHarness();
        var movieID = ID(MetadataEntityType.Movie, "9");

        await harness.Job(movieID.ToString()).Execute();

        harness.Tags.Verify(s => s.RemoveTags(movieID), Times.Once);
        harness.MovieStore.Verify(s => s.RemoveMovie(movieID), Times.Once);
        Assert.Equal([movieID], harness.Provider.CleanedUp);
    }

    [Fact]
    public async Task EveryProviderClaimingTheSourceCleansUpAfterAPurge()
    {
        var harness = new PurgeHarness();
        var other = new FakeProvider();
        harness.Others.Add(other);
        var movieID = ID(MetadataEntityType.Movie, "9");

        await harness.Job(movieID.ToString()).Execute();

        Assert.Equal([movieID], harness.Provider.CleanedUp);
        Assert.Equal([movieID], other.CleanedUp);
    }

    [Fact]
    public async Task APurgeForgetsWhenTheEntryWasLastRefreshed()
    {
        var harness = new PurgeHarness();
        var movieID = ID(MetadataEntityType.Movie, "9");
        var otherID = ID(MetadataEntityType.Movie, "10");
        harness.RefreshState.Times[movieID] = DateTime.Now;
        harness.RefreshState.Times[otherID] = DateTime.Now;

        await harness.Job(movieID.ToString()).Execute();

        Assert.Equal([otherID], harness.RefreshState.Times.Keys);
    }

    [Fact]
    public async Task AnEntryLinkedWhileThePurgeWaitedIsNotPurged()
    {
        var harness = new PurgeHarness();
        var seriesID = ID(MetadataEntityType.Series, "1");
        var held = await harness.Locks.Acquire(seriesID, TestContext.Current.CancellationToken);

        var purge = harness.Job(seriesID.ToString()).Execute();
        harness.Links.Setup(s => s.GetLinksTo(seriesID))
            .Returns([new CrossRef_AniDB_Metadata_Series { Source = Source, AnidbAnimeID = AnimeID, ProviderID = "1" }]);
        held.Dispose();
        await purge;

        harness.SeriesStore.Verify(s => s.RemoveSeries(It.IsAny<MetadataGuid>()), Times.Never);
        Assert.False(harness.Locks.IsInUse(seriesID));
    }

    [Fact]
    public async Task AnEntryStillLinkedIsNotPurged()
    {
        var harness = new PurgeHarness();
        var seriesID = ID(MetadataEntityType.Series, "1");
        harness.Links.Setup(s => s.GetLinksTo(seriesID))
            .Returns([new CrossRef_AniDB_Metadata_Series { Source = Source, AnidbAnimeID = AnimeID, ProviderID = "1" }]);

        await harness.Job(seriesID.ToString()).Execute();

        harness.SeriesStore.Verify(s => s.RemoveSeries(It.IsAny<MetadataGuid>()), Times.Never);
        Assert.Empty(harness.Provider.CleanedUp);
    }

    [Fact]
    public async Task ASeriesEpisodeLinksPointIntoIsNotPurged()
    {
        var harness = new PurgeHarness();
        var seriesID = ID(MetadataEntityType.Series, "1");
        harness.Links.Setup(s => s.GetEpisodeLinksInto(seriesID))
            .Returns([new CrossRef_AniDB_Metadata_Episode { Source = Source, AnidbAnimeID = AnimeID, AnidbEpisodeID = 1, ProviderID = "11", ProviderParentID = "1" }]);

        await harness.Job(seriesID.ToString()).Execute();

        harness.SeriesStore.Verify(s => s.RemoveSeries(It.IsAny<MetadataGuid>()), Times.Never);
        Assert.Empty(harness.Provider.CleanedUp);
    }

    [Fact]
    public async Task ATmdbPurgeLeavesTheStoresAloneAndTmdbCleansUp()
    {
        var harness = new PurgeHarness();
        var tmdbSeries = new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Series, "1");

        await harness.Job(tmdbSeries.ToString()).Execute();
        await harness.Job(new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Series, "1").ToString()).Execute();

        harness.SeriesStore.Verify(s => s.RemoveSeries(It.IsAny<MetadataGuid>()), Times.Never);
        harness.Tags.Verify(s => s.RemoveTags(It.IsAny<MetadataGuid>()), Times.Never);
        Assert.Equal([tmdbSeries], harness.Tmdb.CleanedUp);
        Assert.Empty(harness.Provider.CleanedUp);
    }

    /// <summary>
    /// Stores a user's ordering of a series straight in the ordering tables.
    /// </summary>
    private static void AddOrdering(OrderingTables tables, MetadataGuid seriesID, string orderingID)
    {
        tables.Orderings.Cache.Update(new Metadata_Ordering
        {
            Metadata_OrderingID = tables.Orderings.GetAll().Count + 1,
            Source = MetadataSource.User,
            ProviderID = orderingID,
            SeriesSource = seriesID.Source,
            SeriesID = seriesID.ID,
            Type = OrderingType.User,
            Name = "Mine",
        });
    }

    [Fact]
    public async Task APurgedTmdbShowTakesItsOrderings()
    {
        var harness = new PurgeHarness();
        var show = new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Series, "1");
        var otherShow = new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Series, "2");
        AddOrdering(harness.Orderings, show, "mine");
        AddOrdering(harness.Orderings, otherShow, "theirs");

        await harness.Job(show.ToString()).Execute();

        Assert.Equal(["theirs"], harness.Orderings.Orderings.GetAll().Select(row => row.ProviderID));
        Assert.Equal([show], harness.Tmdb.CleanedUp);
    }

    [Fact]
    public async Task APurgeOfASeriesTheStoreNoLongerHoldsStillTakesItsOrderings()
    {
        var harness = new PurgeHarness();
        var seriesID = ID(MetadataEntityType.Series, "1");
        AddOrdering(harness.Orderings, seriesID, "mine");

        await harness.Job(seriesID.ToString()).Execute();

        Assert.Empty(harness.Orderings.Orderings.GetAll());
        Assert.Equal([seriesID], harness.Provider.CleanedUp);
    }

    [Fact]
    public async Task APurgeOfAFilmLeavesTheOrderingsAlone()
    {
        var harness = new PurgeHarness();
        var seriesID = ID(MetadataEntityType.Series, "9");
        AddOrdering(harness.Orderings, seriesID, "mine");

        await harness.Job(ID(MetadataEntityType.Movie, "9").ToString()).Execute();

        Assert.Single(harness.Orderings.Orderings.GetAll());
    }

    #endregion

    #region Scheduling

    private sealed class SchedulerHarness
    {
        public List<(Type JobType, IQueueJob Job, bool Prioritized)> Queued { get; } = [];

        public Mock<IJobFactory> Jobs { get; } = new();

        public MetadataProviderScheduler Build(Mock<IMetadataCrossReferenceStore> links, params MetadataProviderInfo[] infos)
            => Build(links.Object, infos);

        public MetadataProviderScheduler Build(IMetadataCrossReferenceStore links, params MetadataProviderInfo[] infos)
        {
            var queue = new Mock<IQueueScheduler>();
            queue.Setup(q => q.Enqueue(It.IsAny<Type>(), It.IsAny<Action<IQueueJob>?>(), It.IsAny<bool>()))
                .Returns((Type type, Action<IQueueJob>? configure, bool prioritize) =>
                {
                    var job = (IQueueJob)RuntimeHelpers.GetUninitializedObject(type);
                    configure?.Invoke(job);
                    Queued.Add((type, job, prioritize));
                    return Task.CompletedTask;
                });
            queue.Setup(q => q.Enqueue(It.IsAny<Action<SyncEpisodeLinksJob>?>(), It.IsAny<bool>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
                .Returns((Action<SyncEpisodeLinksJob>? configure, bool prioritize, DateTimeOffset? _, CancellationToken _) =>
                {
                    var job = new SyncEpisodeLinksJob(null!);
                    configure?.Invoke(job);
                    Queued.Add((typeof(SyncEpisodeLinksJob), job, prioritize));
                    return Task.CompletedTask;
                });
            queue.Setup(q => q.Enqueue(It.IsAny<Action<PurgeMetadataJob>?>(), It.IsAny<bool>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
                .Returns((Action<PurgeMetadataJob>? configure, bool prioritize, DateTimeOffset? _, CancellationToken _) =>
                {
                    var job = (PurgeMetadataJob)RuntimeHelpers.GetUninitializedObject(typeof(PurgeMetadataJob));
                    configure?.Invoke(job);
                    Queued.Add((typeof(PurgeMetadataJob), job, prioritize));
                    return Task.CompletedTask;
                });
            return new(Manager(infos).Object, links, queue.Object, Jobs.Object, NullLogger<MetadataProviderScheduler>.Instance);
        }

        public MetadataPurgeService PurgeService(
            Mock<IMetadataCrossReferenceStore> links,
            MetadataProviderInfo[] infos,
            Mock<IMetadataSeriesStore>? seriesStore = null,
            Mock<IMetadataMovieStore>? movieStore = null,
            Mock<IMetadataCollectionStore>? collectionStore = null,
            Mock<IMetadataPeopleStore>? people = null,
            IMetadataRefreshState? refreshState = null,
            Mock<IMetadataStudioStore>? studioStore = null,
            Metadata_Studio[]? studios = null,
            ServerSettings? settings = null,
            Metadata_Creator[]? creators = null,
            Mock<IImageManager>? images = null,
            Metadata_Season[]? seasons = null
        )
        {
            if (people is null)
            {
                people = new Mock<IMetadataPeopleStore>();
                people.Setup(s => s.RemoveOrphaned(It.IsAny<MetadataSource>(), It.IsAny<DateTime>())).Returns([]);
            }

            studioStore ??= new Mock<IMetadataStudioStore>();
            studioStore.Setup(s => s.RemoveOrphaned(It.IsAny<MetadataSource>(), It.IsAny<DateTime>())).Returns([]);
            images ??= new Mock<IImageManager>();
            images.Setup(i => i.GetImageCrossReferencesForEntity(It.IsAny<IWithImages>(), It.IsAny<ImageCrossReferenceFilteringOptions?>())).Returns([]);
            seriesStore ??= new Mock<IMetadataSeriesStore>();
            movieStore ??= new Mock<IMetadataMovieStore>();
            collectionStore ??= new Mock<IMetadataCollectionStore>();
            var metadata = new Mock<IMetadataService>();
            metadata.Setup(m => m.GetAllSeriesForSource(It.IsAny<MetadataSource>()))
                .Returns((MetadataSource source) => seriesStore.Object.GetAllSeries(source) ?? []);
            metadata.Setup(m => m.GetAllMoviesForSource(It.IsAny<MetadataSource>()))
                .Returns((MetadataSource source) => movieStore.Object.GetAllMovies(source) ?? []);
            metadata.Setup(m => m.GetAllCollectionsForSource(It.IsAny<MetadataSource>()))
                .Returns((MetadataSource source) => collectionStore.Object.GetAllCollections(source) ?? []);
            return new(
                Manager(infos).Object,
                links.Object,
                metadata.Object,
                seriesStore.Object,
                collectionStore.Object,
                people.Object,
                studioStore.Object,
                CachedRepo.Build<Metadata_CreatorRepository, int, Metadata_Creator>(row => row.Metadata_CreatorID, creators ?? []),
                CachedRepo.Build<Metadata_CharacterRepository, int, Metadata_Character>(row => row.Metadata_CharacterID),
                CachedRepo.Build<Metadata_StudioRepository, int, Metadata_Studio>(row => row.Metadata_StudioID, studios ?? []),
                CachedRepo.Build<Metadata_NetworkRepository, int, Metadata_Network>(row => row.Metadata_NetworkID),
                CachedRepo.Build<Metadata_SeriesRepository, int, Metadata_Series>(row => row.Metadata_SeriesID),
                CachedRepo.Build<Metadata_SeasonRepository, int, Metadata_Season>(row => row.Metadata_SeasonID, seasons ?? []),
                CachedRepo.Build<Metadata_EpisodeRepository, int, Metadata_Episode>(row => row.Metadata_EpisodeID),
                new MetadataEntityCleanup(
                    people.Object,
                    Mock.Of<IMetadataTagStore>(),
                    studioStore.Object,
                    Mock.Of<IMetadataRelationStore>(),
                    Mock.Of<IMetadataSuggestionStore>(),
                    images.Object
                ),
                refreshState ?? new FakeRefreshState(),
                new MetadataEntryLocks(),
                Build(links, infos),
                new StubSettingsProvider(settings ?? new()),
                NullLogger<MetadataPurgeService>.Instance
            );
        }

        public MetadataRefreshService Service(
            Mock<IMetadataCrossReferenceStore> links,
            MetadataProviderInfo[] infos,
            Mock<IMetadataCollectionStore>? collections = null,
            IMetadataService? metadata = null,
            int[]? notInLibrary = null,
            MetadataImageContributorScheduler? contributors = null
        )
        {
            if (metadata is null)
            {
                // Every anime is in the library but the ones named, and the
                // stored collections are the collection store's.
                var mock = new Mock<IMetadataService>();
                mock.Setup(m => m.GetShokoSeriesByAnidbID(It.IsAny<int>()))
                    .Returns((int animeID) => notInLibrary?.Contains(animeID) is true ? null : Mock.Of<IShokoSeries>(series => series.AnidbAnimeID == animeID));
                mock.Setup(m => m.GetAllCollectionsForSource(It.IsAny<MetadataSource>()))
                    .Returns((MetadataSource source) => collections?.Object.GetAllCollections(source) ?? []);
                metadata = mock.Object;
            }

            return new(
                Manager(infos).Object,
                links.Object,
                metadata,
                Build(links, infos),
                new MetadataEntryLocks(),
                new FakeRefreshState(),
                contributors ?? new MetadataImageContributorScheduler(
                    Mock.Of<IMetadataImageContributorManager>(m => m.ImageContributors == new List<MetadataImageContributorInfo>()),
                    Mock.Of<IQueueScheduler>(),
                    NullLogger<MetadataImageContributorScheduler>.Instance
                )
            );
        }
    }

    [Fact]
    public async Task ARefreshForAnAnimeGoesToTheLinkedEnabledProviders()
    {
        var harness = new SchedulerHarness();
        var scheduler = harness.Build(
            Links(series: ["1"]),
            Info(new FakeProvider()),
            Info(new SeriesOnlyProvider(), enabled: false),
            Info(new FakeTmdbProvider())
        );

        await scheduler.ScheduleRefreshForAnime(AnimeID, force: true, cancellationToken: TestContext.Current.CancellationToken);

        var (type, job, prioritized) = Assert.Single(harness.Queued);
        Assert.Equal(typeof(RefreshMetadataJob<FakeProvider>), type);
        var refresh = (IMetadataRefreshJob)job;
        Assert.Equal(AnimeID, refresh.AnimeID);
        Assert.Null(refresh.EntryID);
        Assert.True(refresh.Force);
        Assert.Equal(MetadataRefreshReason.Requested, refresh.Reason);
        Assert.True(prioritized);
    }

    [Fact]
    public async Task AScheduledRefreshForAnAnimeIsNotForced()
    {
        var harness = new SchedulerHarness();
        var scheduler = harness.Build(Links(series: ["1"]), Info(new FakeProvider()));

        await scheduler.ScheduleRefreshForAnime(AnimeID, cancellationToken: TestContext.Current.CancellationToken);

        var (_, job, prioritized) = Assert.Single(harness.Queued);
        var refresh = (IMetadataRefreshJob)job;
        Assert.False(refresh.Force);
        Assert.Equal(MetadataRefreshReason.Scheduled, refresh.Reason);
        Assert.False(prioritized);
    }

    [Fact]
    public async Task ALinkSyncIsQueuedForAPluginSeriesOnly()
    {
        var harness = new SchedulerHarness();
        var scheduler = harness.Build(Links(), Info(new FakeProvider()));
        var token = TestContext.Current.CancellationToken;

        await scheduler.ScheduleLinkSync(ID(MetadataEntityType.Series, "1"), token);
        await scheduler.ScheduleLinkSync(ID(MetadataEntityType.Movie, "9"), token);
        await scheduler.ScheduleLinkSync(new(MetadataSource.TMDB, MetadataEntityType.Series, "1"), token);

        var (type, job, _) = Assert.Single(harness.Queued);
        Assert.Equal(typeof(SyncEpisodeLinksJob), type);
        Assert.Equal("1", ((SyncEpisodeLinksJob)job).SeriesID);
    }

    [Fact]
    public async Task AnAnimeNotLinkedOnASourceIsNotRefreshedFromIt()
    {
        var harness = new SchedulerHarness();
        var scheduler = harness.Build(Links(), Info(new FakeProvider()));

        Assert.False(await scheduler.ScheduleRefresh(Info(new FakeProvider()), AnimeID, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Empty(harness.Queued);
    }

    [Fact]
    public async Task ASearchGoesToTheAutoLinkersJobTmdbsIncluded()
    {
        var harness = new SchedulerHarness();
        var scheduler = harness.Build(Links(), Info(new FakeProvider()), Info(new FakeTmdbProvider()));

        Assert.True(await scheduler.ScheduleSearch(Source, AnimeID, force: true, cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(await scheduler.ScheduleSearch(MetadataSource.TMDB, AnimeID, cancellationToken: TestContext.Current.CancellationToken));
        Assert.False(await scheduler.ScheduleSearch(TestSources.AniList, AnimeID, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(
            [typeof(SearchMetadataJob<FakeProvider>), typeof(SearchMetadataJob<FakeTmdbProvider>)],
            harness.Queued.Select(queued => queued.JobType)
        );
        Assert.True(((IMetadataSearchJob)harness.Queued[0].Job).Force);
        Assert.False(((IMetadataSearchJob)harness.Queued[0].Job).Replace);
        Assert.False(((IMetadataSearchJob)harness.Queued[1].Job).Force);
    }

    [Fact]
    public async Task NoSearchIsQueuedForAnUnconfiguredAutoLinker()
    {
        var harness = new SchedulerHarness();
        var provider = new FakeProvider { IsConfigured = false };
        var scheduler = harness.Build(Links(), Info(provider));

        Assert.False(await scheduler.ScheduleSearch(Source, AnimeID, cancellationToken: TestContext.Current.CancellationToken));
        Assert.False(await scheduler.ScheduleSearch(Source, AnimeID, force: true, replace: true, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Empty(harness.Queued);

        provider.IsConfigured = true;
        Assert.True(await scheduler.ScheduleSearch(Source, AnimeID, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Single(harness.Queued);
    }

    [Fact]
    public async Task APurgeGoesToTheCoreJobForAPluginSourceOrTmdb()
    {
        var harness = new SchedulerHarness();
        var scheduler = harness.Build(Links(), Info(new FakeProvider()), Info(new FakeTmdbProvider()));
        var pluginEntry = ID(MetadataEntityType.Series, "1");
        var tmdbEntry = new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Movie, "2");

        Assert.True(await scheduler.SchedulePurge(pluginEntry, cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(await scheduler.SchedulePurge(tmdbEntry, cancellationToken: TestContext.Current.CancellationToken));
        Assert.False(await scheduler.SchedulePurge(new(MetadataSource.AniDB, MetadataEntityType.Series, "3"), cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal([pluginEntry.ToString(), tmdbEntry.ToString()], harness.Queued.Select(queued => ((PurgeMetadataJob)queued.Job).EntryID));
        Assert.All(harness.Queued, queued => Assert.False(((PurgeMetadataJob)queued.Job).Force));
        Assert.All(harness.Queued, queued => Assert.False(queued.Prioritized));
    }

    [Fact]
    public async Task ImagesAreQueuedForAnEntryOnTheProvidersSourceOnly()
    {
        var harness = new SchedulerHarness();
        var info = Info(new FakeProvider());
        var scheduler = harness.Build(Links(series: ["1"], movies: ["9"]), info, Info(new SeriesOnlyProvider()));
        var token = TestContext.Current.CancellationToken;

        Assert.True(await scheduler.ScheduleImages(info, ID(MetadataEntityType.Series, "1"), force: true, token));
        Assert.False(await scheduler.ScheduleImages(info, new(TestSources.AniList, MetadataEntityType.Series, "1"), cancellationToken: token));
        Assert.False(await scheduler.ScheduleImages(Info(new SeriesOnlyProvider()), ID(MetadataEntityType.Series, "1"), cancellationToken: token));
        Assert.Equal(2, await scheduler.ScheduleImagesForAnime(info, AnimeID, cancellationToken: token));

        Assert.All(harness.Queued, queued => Assert.Equal(typeof(DownloadMetadataImagesJob<FakeProvider>), queued.JobType));
        Assert.Equal(
            [ID(MetadataEntityType.Series, "1").ToString(), ID(MetadataEntityType.Series, "1").ToString(), ID(MetadataEntityType.Movie, "9").ToString()],
            harness.Queued.Select(queued => ((IMetadataImagesJob)queued.Job).EntryID)
        );
        Assert.True(((IMetadataImagesJob)harness.Queued[0].Job).Force);
        Assert.True(harness.Queued[0].Prioritized);
        Assert.False(((IMetadataImagesJob)harness.Queued[1].Job).Force);
    }

    [Fact]
    public async Task ImagesAreQueuedOnlyForTheKindsTheProviderIsEnabledFor()
    {
        var harness = new SchedulerHarness();
        var info = Info(new FakeProvider(), entityTypes: [MetadataEntityType.Movie]);
        var scheduler = harness.Build(Links(series: ["1"], movies: ["9"]), info);
        var token = TestContext.Current.CancellationToken;

        Assert.False(await scheduler.ScheduleImages(info, ID(MetadataEntityType.Series, "1"), cancellationToken: token));
        Assert.Equal(1, await scheduler.ScheduleImagesForAnime(info, AnimeID, cancellationToken: token));

        Assert.Equal([ID(MetadataEntityType.Movie, "9").ToString()], harness.Queued.Select(queued => ((IMetadataImagesJob)queued.Job).EntryID));
    }

    [Fact]
    public async Task ARefreshCanBeQueuedAsAQuickOne()
    {
        var harness = new SchedulerHarness();
        var info = Info(new FakeProvider());
        var scheduler = harness.Build(Links(series: ["1"]), info);

        await scheduler.ScheduleRefresh(info, AnimeID, options: new() { QuickRefresh = true }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(((IMetadataRefreshJob)Assert.Single(harness.Queued).Job).QuickRefresh);
    }

    [Fact]
    public async Task TheSupplementarySchedulerRefreshesALinkedSourceAndSearchesAnUnlinkedOne()
    {
        var harness = new SchedulerHarness();
        var linked = harness.Build(Links(series: ["1"]), Info(new FakeProvider()), Info(new FakeTmdbProvider()));
        var unlinked = new SchedulerHarness();
        var metadata = new Mock<IMetadataService>();
        metadata.Setup(m => m.GetShokoSeriesByAnidbID(AnimeID)).Returns(new Mock<IShokoSeries>().Object);

        await new SupplementaryMetadataScheduler(Manager(Info(new FakeProvider()), Info(new FakeTmdbProvider())).Object, linked, metadata.Object)
            .ScheduleForAnime(AnimeID);
        await new SupplementaryMetadataScheduler(Manager(Info(new FakeProvider())).Object, unlinked.Build(Links(), Info(new FakeProvider())), metadata.Object)
            .ScheduleForAnime(AnimeID);

        // TMDB is not linked, so it is searched like any other source.
        Assert.Equal(
            [typeof(RefreshMetadataJob<FakeProvider>), typeof(SearchMetadataJob<FakeTmdbProvider>)],
            harness.Queued.Select(queued => queued.JobType)
        );
        Assert.Equal([typeof(SearchMetadataJob<FakeProvider>)], unlinked.Queued.Select(queued => queued.JobType));
    }

    [Fact]
    public async Task ARefreshOfOneEntryCarriesTheOptionsAndAForcedOneIsNeverScheduled()
    {
        var harness = new SchedulerHarness();
        var infos = new[] { Info(new FakeProvider()) };
        var service = harness.Service(Links(series: ["1"]), infos);
        var series = ID(MetadataEntityType.Series, "1");

        Assert.True(await service.RefreshEntry(
            series,
            force: true,
            options: new() { DownloadCrewAndCast = true, DownloadAlternateOrdering = false, DownloadNetworks = true, DownloadCollections = false, QuickRefresh = true },
            cancellationToken: TestContext.Current.CancellationToken
        ));
        Assert.True(await service.RefreshEntry(series, cancellationToken: TestContext.Current.CancellationToken));

        var forced = (IMetadataRefreshJob)harness.Queued[0].Job;
        Assert.Equal(series.ToString(), forced.EntryID);
        Assert.True(forced.Force);
        Assert.False(forced.AllowUnlinked);
        Assert.True(forced.QuickRefresh);
        Assert.False(forced.DownloadImages);
        Assert.True(forced.DownloadCrewAndCast);
        Assert.False(forced.DownloadAlternateOrdering);
        Assert.True(forced.DownloadNetworks);
        Assert.False(forced.DownloadCollections);
        Assert.Equal(MetadataRefreshReason.Requested, forced.Reason);
        Assert.True(harness.Queued[0].Prioritized);

        // Left out, the options are a full refresh with images.
        var plain = (IMetadataRefreshJob)harness.Queued[1].Job;
        Assert.True(plain.DownloadImages);
        Assert.False(plain.QuickRefresh);
        Assert.Null(plain.DownloadCrewAndCast);
        Assert.Equal(MetadataRefreshReason.Scheduled, plain.Reason);
        Assert.False(plain.AllowUnlinked);

        // Only a caller saying somebody asked for the entry has it fetched
        // whether or not it is linked.
        Assert.True(await service.RefreshEntry(
            series,
            options: new() { Reason = MetadataRefreshReason.Requested },
            cancellationToken: TestContext.Current.CancellationToken
        ));
        Assert.True(((IMetadataRefreshJob)harness.Queued[2].Job).AllowUnlinked);
        Assert.False(harness.Queued[2].Prioritized);

        // Somebody waiting on a paused provider gets theirs queued first.
        Assert.True(await service.RefreshEntry(series, prioritize: true, cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(harness.Queued[3].Prioritized);
        Assert.False(((IMetadataRefreshJob)harness.Queued[3].Job).Force);
    }

    [Fact]
    public async Task AnImmediateRefreshRunsAtOnceUnlessTheProviderIsPaused()
    {
        var harness = new SchedulerHarness();
        var provider = new PausableProvider();
        var infos = new[] { Info(provider, entityTypes: [MetadataEntityType.Series]) };
        var service = harness.Service(Links(series: ["1"]), infos);
        var series = ID(MetadataEntityType.Series, "1");
        var ran = new List<IMetadataRefreshJob>();
        harness.Jobs.Setup(j => j.Execute(It.IsAny<Action<RefreshMetadataJob<PausableProvider>>?>()))
            .Returns((Action<RefreshMetadataJob<PausableProvider>>? configure) =>
            {
                var job = (RefreshMetadataJob<PausableProvider>)RuntimeHelpers.GetUninitializedObject(typeof(RefreshMetadataJob<PausableProvider>));
                configure?.Invoke(job);
                ran.Add(job);
                return Task.CompletedTask;
            });

        Assert.True(await service.RefreshEntry(series, immediate: true, cancellationToken: TestContext.Current.CancellationToken));
        provider.PauseStatus = new() { IsPaused = true, Reason = "Rate limited." };
        Assert.False(await service.RefreshEntry(series, immediate: true, cancellationToken: TestContext.Current.CancellationToken));
        harness.Jobs.Setup(j => j.Execute(It.IsAny<Action<RefreshMetadataJob<PausableProvider>>?>()))
            .ThrowsAsync(new JobBlockedException(typeof(RefreshMetadataJob<PausableProvider>)));
        provider.PauseStatus = MetadataProviderPauseStatus.NotPaused;
        Assert.False(await service.RefreshEntry(series, immediate: true, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(series.ToString(), Assert.Single(ran).EntryID);
        Assert.Empty(harness.Queued);
    }

    [Fact]
    public async Task DownloadingAnEntrysImagesQueuesTheContributorsAtOnceOnlyWhenNoProviderCoversIt()
    {
        var harness = new SchedulerHarness();
        var provider = new FakeProvider();
        var series = ID(MetadataEntityType.Series, "1");
        var contributors = new Mock<IMetadataImageContributorManager>();
        contributors.SetupGet(m => m.ImageContributors).Returns([ContributorInfo(new ImageOnlyContributor())]);
        var contributorJobs = new List<(Type JobType, string EntryID)>();
        var covered = harness.Service(Links(series: ["1"]), [Info(provider)], contributors: ContributorScheduler(contributors, contributorJobs));
        var seriesOff = harness.Service(
            Links(series: ["1"]),
            [Info(provider, entityTypes: [MetadataEntityType.Movie])],
            contributors: ContributorScheduler(contributors, contributorJobs)
        );

        // The owner's job queues the contributors once it has run.
        Assert.True(await covered.DownloadImages(series, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(typeof(DownloadMetadataImagesJob<FakeProvider>), Assert.Single(harness.Queued).JobType);
        Assert.Empty(contributorJobs);

        // An owner's job refused a run at once queues no contributor either.
        harness.Jobs.Setup(j => j.Execute(It.IsAny<Action<DownloadMetadataImagesJob<FakeProvider>>?>()))
            .ThrowsAsync(new JobBlockedException(typeof(DownloadMetadataImagesJob<FakeProvider>)));
        Assert.False(await covered.DownloadImages(series, immediate: true, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Empty(contributorJobs);

        // With no provider on for the entry's kind, the contributors are queued at once.
        Assert.True(await seriesOff.DownloadImages(series, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal([(typeof(DownloadContributedImagesJob<ImageOnlyContributor>), series.ToString())], contributorJobs);
        Assert.Single(harness.Queued);
    }

    [Fact]
    public void ThePauseStatusOfASourceIsTheOneResumingLast()
    {
        var harness = new SchedulerHarness();
        var first = new PausableProvider();
        var second = new PausableProvider();
        var infos = new[] { Info(first, entityTypes: [MetadataEntityType.Series]), Info(second, entityTypes: [MetadataEntityType.Series]) };
        var service = harness.Service(Links(), infos);

        Assert.Same(MetadataProviderPauseStatus.NotPaused, service.GetPauseStatus(Source));
        Assert.False(service.GetPauseStatus(TestSources.AniList).IsPaused);

        var soon = DateTime.UtcNow.AddMinutes(1);
        var later = DateTime.UtcNow.AddMinutes(5);
        first.PauseStatus = new() { IsPaused = true, Reason = "Soon.", ResumesAt = soon };
        second.PauseStatus = new() { IsPaused = true, Reason = "Later.", ResumesAt = later };

        var status = service.GetPauseStatus(Source);
        Assert.True(status.IsPaused);
        Assert.Equal("Later.", status.Reason);
        Assert.Equal(later, status.ResumesAt);
    }

    [Fact]
    public async Task ASearchOfTheLibrarySkipsWhatAScheduledSearchWouldUnlessForced_AndNeverReplacesLinks()
    {
        var harness = new SchedulerHarness();
        var infos = new[] { Info(new FakeProvider()) };
        var links = Links(series: ["1"]);
        IShokoSeries Series(int animeID, bool vetoed = false, bool restricted = false)
        {
            var series = new Mock<IShokoSeries>();
            series.SetupGet(s => s.AnidbAnimeID).Returns(animeID);
            series.Setup(s => s.IsAutoLinkingDisabled(Source)).Returns(vetoed);
            series.SetupGet(s => s.AnidbAnime).Returns(Mock.Of<Abstractions.Metadata.Anidb.IAnidbAnime>(anime => anime.Restricted == restricted));
            return series.Object;
        }

        var metadata = new Mock<IMetadataService>();
        metadata.Setup(m => m.GetAllShokoSeries()).Returns([Series(AnimeID), Series(200), Series(300, vetoed: true), Series(400, restricted: true)]);
        var service = harness.Service(links, infos, metadata: metadata.Object);

        ((FakeProvider)infos[0].Provider).IsConfigured = false;
        Assert.Equal(0, await service.AutoSearchAll(Source, force: true, cancellationToken: TestContext.Current.CancellationToken));
        ((FakeProvider)infos[0].Provider).IsConfigured = true;

        Assert.Equal(1, await service.AutoSearchAll(Source, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(3, await service.AutoSearchAll(Source, force: true, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(0, await service.AutoSearchAll(TestSources.AniList, force: true, cancellationToken: TestContext.Current.CancellationToken));

        // The linked anime is left alone even when forced, since a search of
        // the whole library never replaces what a person may have verified.
        Assert.Equal([200, 200, 300, 400], harness.Queued.Select(queued => ((IMetadataSearchJob)queued.Job).AnimeID));
        Assert.All(harness.Queued, queued => Assert.False(((IMetadataSearchJob)queued.Job).Replace));
    }

    [Fact]
    public async Task TheUnusedSweepSkipsWhatWasRefreshedSinceTheCutoff()
    {
        var harness = new SchedulerHarness();
        var infos = new[] { Info(new FakeProvider()) };
        var links = Links();
        var fresh = ID(MetadataEntityType.Series, "fresh");
        var stale = ID(MetadataEntityType.Series, "stale");
        var never = ID(MetadataEntityType.Movie, "never");
        var seriesStore = new Mock<IMetadataSeriesStore>();
        seriesStore.Setup(s => s.GetAllSeries(Source)).Returns([Entity<ISeries>(fresh), Entity<ISeries>(stale)]);
        var movieStore = new Mock<IMetadataMovieStore>();
        movieStore.Setup(s => s.GetAllMovies(Source)).Returns([Entity<IMovie>(never)]);
        var collectionStore = new Mock<IMetadataCollectionStore>();
        collectionStore.Setup(s => s.GetAllCollections(Source)).Returns([]);
        var refreshState = new FakeRefreshState();
        refreshState.Times[fresh] = DateTime.Now.AddDays(-1);
        refreshState.Times[stale] = DateTime.Now.AddDays(-30);
        var service = harness.PurgeService(links, infos, seriesStore, movieStore, collectionStore, refreshState: refreshState);

        Assert.Equal(2, await service.PurgeUnused(Source, DateTime.Now.AddDays(-14), cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(0, await service.PurgeUnused(MetadataSource.TMDB, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(1, await service.PurgeUnused(Source, DateTime.Now.AddDays(-14), MetadataEntityType.Movie, TestContext.Current.CancellationToken));

        Assert.Equal(
            [stale.ToString(), never.ToString(), never.ToString()],
            harness.Queued.Select(queued => ((PurgeMetadataJob)queued.Job).EntryID)
        );
    }

    [Fact]
    public async Task TheUnusedSweepGoesByWhenAnEntryNeverRefreshedWasStored()
    {
        var harness = new SchedulerHarness();
        var infos = new[] { Info(new FakeProvider()) };
        var seriesStore = new Mock<IMetadataSeriesStore>();
        seriesStore.Setup(s => s.GetAllSeries(Source)).Returns(
        [
            new Metadata_Series { Source = Source, ProviderID = "previewed", LastUpdatedAt = DateTime.Now.AddMinutes(-5) },
            new Metadata_Series { Source = Source, ProviderID = "forgotten", LastUpdatedAt = DateTime.Now.AddDays(-30) },
        ]);
        var movieStore = new Mock<IMetadataMovieStore>();
        movieStore.Setup(s => s.GetAllMovies(Source)).Returns([]);
        var collectionStore = new Mock<IMetadataCollectionStore>();
        collectionStore.Setup(s => s.GetAllCollections(Source)).Returns([]);
        var service = harness.PurgeService(Links(), infos, seriesStore, movieStore, collectionStore, refreshState: new FakeRefreshState());

        Assert.Equal(1, await service.PurgeUnused(Source, DateTime.Now.AddDays(-14), cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(ID(MetadataEntityType.Series, "forgotten").ToString(), ((PurgeMetadataJob)Assert.Single(harness.Queued).Job).EntryID);
    }

    [Fact]
    public async Task PurgingASourcesCollectionsForcesEachOne()
    {
        var harness = new SchedulerHarness();
        var infos = new[] { Info(new FakeProvider()) };
        var collectionStore = new Mock<IMetadataCollectionStore>();
        var collectionID = ID(MetadataEntityType.Collection, "c");
        collectionStore.Setup(s => s.GetAllCollections(Source)).Returns([Entity<ICollection>(collectionID)]);
        var service = harness.PurgeService(Links(), infos, collectionStore: collectionStore);

        Assert.Equal(1, await service.PurgeCollections(Source, TestContext.Current.CancellationToken));
        Assert.True(await service.PurgeEntry(ID(MetadataEntityType.Series, "1"), force: true, TestContext.Current.CancellationToken));

        Assert.All(harness.Queued, queued => Assert.True(((PurgeMetadataJob)queued.Job).Force));
        Assert.All(harness.Queued, queued => Assert.True(queued.Prioritized));
        Assert.Equal(collectionID.ToString(), ((PurgeMetadataJob)harness.Queued[0].Job).EntryID);
    }

    [Fact]
    public async Task TheOrphanPurgeTakesTheStudiosAndNetworksOfASourceWithoutPeople()
    {
        var harness = new SchedulerHarness();
        var studioStore = new Mock<IMetadataStudioStore>();
        var images = new Mock<IImageManager>();
        var studio = new MetadataGuid(TestSources.AniList, MetadataEntityType.Studio, "7");
        var network = new MetadataGuid(TestSources.AniList, MetadataEntityType.Network, "3");
        var service = harness.PurgeService(
            Links(),
            [Info(new FakeProvider())],
            studioStore: studioStore,
            studios: [new() { Metadata_StudioID = 1, Source = TestSources.AniList, ProviderID = "7" }],
            images: images
        );
        studioStore.Setup(s => s.RemoveOrphaned(TestSources.AniList, It.IsAny<DateTime>())).Returns([studio, network]);

        Assert.Equal(2, await service.PurgeOrphaned(cancellationToken: TestContext.Current.CancellationToken));

        studioStore.Verify(s => s.RemoveOrphaned(TestSources.AniList, It.IsAny<DateTime>()), Times.Once);
        images.Verify(
            i => i.GetImageCrossReferencesForEntity(It.Is<IWithImages>(entity => entity.ID == network), It.IsAny<ImageCrossReferenceFilteringOptions?>()),
            Times.Once
        );
        Assert.Equal(0, await service.PurgeOrphaned(MetadataSource.Shoko, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheOrphanPurgeRemovesAnUnlinkedSeriesLeftoverAndRefreshesALinkedOne()
    {
        var harness = new SchedulerHarness();
        var seriesStore = new Mock<IMetadataSeriesStore>();
        var service = harness.PurgeService(
            Links(series: ["linked"]),
            [Info(new FakeProvider())],
            seriesStore: seriesStore,
            seasons:
            [
                new() { Metadata_SeasonID = 1, Source = Source, ProviderID = "gone-1", SeriesID = "gone" },
                new() { Metadata_SeasonID = 2, Source = Source, ProviderID = "linked-1", SeriesID = "linked" },
            ]
        );

        await service.PurgeOrphaned(cancellationToken: TestContext.Current.CancellationToken);

        seriesStore.Verify(s => s.RemoveSeries(ID(MetadataEntityType.Series, "gone")), Times.Once);
        seriesStore.Verify(s => s.RemoveSeries(ID(MetadataEntityType.Series, "linked")), Times.Never);
        var refresh = Assert.Single(harness.Queued);
        Assert.Equal(typeof(RefreshMetadataJob<FakeProvider>), refresh.JobType);
        Assert.Equal(ID(MetadataEntityType.Series, "linked").ToString(), ((IMetadataRefreshJob)refresh.Job).EntryID);
    }

    [Fact]
    public async Task TheOrphanedPeoplePurgeGoesByTheSettingAndUnlinksTheirImages()
    {
        var harness = new SchedulerHarness();
        var people = new Mock<IMetadataPeopleStore>();
        var images = new Mock<IImageManager>();
        var settings = new ServerSettings();
        settings.Metadata.PurgeOrphanedAfterDays = 3;
        var gone = ID(MetadataEntityType.Creator, "gone");
        var cutoffs = new List<DateTime>();
        people.Setup(s => s.RemoveOrphaned(It.IsAny<MetadataSource>(), It.IsAny<DateTime>()))
            .Returns((MetadataSource source, DateTime cutoff) =>
            {
                cutoffs.Add(cutoff);
                return source == Source ? [gone] : [];
            });
        var tmdb = new FakeTmdbProvider { PeoplePurged = 2 };
        var service = harness.PurgeService(
            Links(),
            [Info(new FakeProvider()), Info(tmdb)],
            people: people,
            settings: settings,
            creators:
            [
                new() { Metadata_CreatorID = 1, Source = Source, ProviderID = "gone" },
                new() { Metadata_CreatorID = 2, Source = TestSources.AniList, ProviderID = "kept" },
            ],
            images: images
        );

        // TMDB's provider purges TMDB's own people, by the same cutoff.
        Assert.Equal(3, await service.PurgeOrphaned(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(2, await service.PurgeOrphaned(MetadataSource.TMDB, cancellationToken: TestContext.Current.CancellationToken));
        var exact = DateTime.Now.AddDays(-30);
        Assert.Equal(1, await service.PurgeOrphaned(Source, exact, TestContext.Current.CancellationToken));

        people.Verify(s => s.RemoveOrphaned(Source, It.IsAny<DateTime>()), Times.Exactly(2));
        people.Verify(s => s.RemoveOrphaned(TestSources.AniList, It.IsAny<DateTime>()), Times.Once);
        people.Verify(s => s.RemoveOrphaned(MetadataSource.TMDB, It.IsAny<DateTime>()), Times.Never);
        Assert.InRange(cutoffs[0], DateTime.Now.AddDays(-3).AddMinutes(-1), DateTime.Now.AddDays(-3));
        Assert.Equal(exact, cutoffs[^1]);
        Assert.Equal(2, tmdb.PeoplePurgedBefore.Count);
        Assert.All(tmdb.PeoplePurgedBefore, cutoff => Assert.InRange(cutoff, DateTime.Now.AddDays(-3).AddMinutes(-1), DateTime.Now.AddDays(-3)));
        images.Verify(i => i.GetImageCrossReferencesForEntity(It.Is<IWithImages>(entity => entity.ID == gone), It.IsAny<ImageCrossReferenceFilteringOptions?>()), Times.Exactly(2));
    }

    #endregion

    #region Actions

    private static T InSeries<T>(T action, int anidbAnimeID) where T : SeriesAction
    {
        var series = new Mock<IShokoSeries>();
        series.SetupGet(s => s.AnidbAnimeID).Returns(anidbAnimeID);
        return InSeries(action, series.Object);
    }

    private static T InSeries<T>(T action, IShokoSeries series) where T : SeriesAction
    {
        typeof(SeriesAction).GetInterfaces().Single(type => type.Name == "IScopedAction")
            .GetMethod("SetContext")!
            .Invoke(action, [series]);
        return action;
    }

    [Fact]
    public async Task OneEntryIsQueuedForTheProviderRefreshingItsKind()
    {
        var harness = new SchedulerHarness();
        var infos = new[] { Info(new SeriesOnlyProvider()), Info(new FakeProvider()), Info(new FakeTmdbProvider()) };
        var scheduler = harness.Build(Links(), infos);
        var collectionID = ID(MetadataEntityType.Collection, "c");

        Assert.True(await scheduler.ScheduleRefreshForEntry(collectionID, force: true, cancellationToken: TestContext.Current.CancellationToken));
        var tmdbSeries = new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Series, "1");
        var tmdbCollection = new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Collection, "1");
        var unservedSeries = new MetadataGuid(TestSources.AniList, MetadataEntityType.Series, "1");
        Assert.True(await scheduler.ScheduleRefreshForEntry(tmdbSeries, cancellationToken: TestContext.Current.CancellationToken));
        Assert.False(await scheduler.ScheduleRefreshForEntry(tmdbCollection, cancellationToken: TestContext.Current.CancellationToken));
        Assert.False(await scheduler.ScheduleRefreshForEntry(unservedSeries, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal([typeof(RefreshMetadataJob<FakeProvider>), typeof(RefreshMetadataJob<FakeTmdbProvider>)], harness.Queued.Select(queued => queued.JobType));
        var refresh = (IMetadataRefreshJob)harness.Queued[0].Job;
        Assert.Equal(collectionID.ToString(), refresh.EntryID);
        Assert.Equal(0, refresh.AnimeID);
        Assert.True(refresh.Force);
        Assert.Equal(MetadataRefreshReason.Requested, refresh.Reason);
        Assert.True(harness.Queued[0].Prioritized);
        Assert.Equal(tmdbSeries.ToString(), ((IMetadataRefreshJob)harness.Queued[1].Job).EntryID);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TheSeriesSearchActionSearchesEverySourceWithAnEnabledAutoLinker(bool force)
    {
        var harness = new SchedulerHarness();
        var infos = new[]
        {
            Info(new FakeProvider(), autoLink: false),
            Info(new FakeTmdbProvider()),
            Info(new FakeProvider(), enabled: false, source: TestSources.AniList),
        };
        var action = InSeries(new AutoLinkMetadataSeriesAction(Manager(infos).Object, harness.Build(Links(), infos)), AnimeID);
        action.Force = force;

        await action.Execute(TestContext.Current.CancellationToken);

        Assert.Equal([typeof(SearchMetadataJob<FakeProvider>), typeof(SearchMetadataJob<FakeTmdbProvider>)], harness.Queued.Select(queued => queued.JobType));
        Assert.All(harness.Queued, queued => Assert.Equal(force, ((IMetadataSearchJob)queued.Job).Replace));
    }

    [Fact]
    public async Task TheSeriesSearchActionSkipsASourceTheSeriesIsLeftAloneOnUnlessForced()
    {
        var harness = new SchedulerHarness();
        var infos = new[] { Info(new FakeProvider()), Info(new FakeTmdbProvider()) };
        var series = new Mock<IShokoSeries>();
        series.SetupGet(s => s.AnidbAnimeID).Returns(AnimeID);
        series.Setup(s => s.IsAutoLinkingDisabled(MetadataSource.TMDB)).Returns(true);
        var action = InSeries(new AutoLinkMetadataSeriesAction(Manager(infos).Object, harness.Build(Links(), infos)), series.Object);

        await action.Execute(TestContext.Current.CancellationToken);
        action.Force = true;
        await action.Execute(TestContext.Current.CancellationToken);

        Assert.Equal(
            [typeof(SearchMetadataJob<FakeProvider>), typeof(SearchMetadataJob<FakeProvider>), typeof(SearchMetadataJob<FakeTmdbProvider>)],
            harness.Queued.Select(queued => queued.JobType)
        );
    }

    [Fact]
    public async Task ThePurgeActionPurgesOnlyWhatNothingLinksTo()
    {
        var harness = new SchedulerHarness();
        var infos = new[] { Info(new FakeProvider()) };
        var links = Links();
        var linked = ID(MetadataEntityType.Series, "1");
        var unlinked = ID(MetadataEntityType.Series, "2");
        var episodeLinked = ID(MetadataEntityType.Series, "3");
        links.Setup(s => s.GetLinksTo(linked)).Returns([new CrossRef_AniDB_Metadata_Series { Source = Source, AnidbAnimeID = AnimeID, ProviderID = "1" }]);
        links.Setup(s => s.GetEpisodeLinksInto(episodeLinked))
            .Returns([new CrossRef_AniDB_Metadata_Episode { Source = Source, AnidbAnimeID = AnimeID, AnidbEpisodeID = 1, ProviderID = "31", ProviderParentID = "3" }]);
        var seriesStore = new Mock<IMetadataSeriesStore>();
        seriesStore.Setup(s => s.GetAllSeries(Source))
            .Returns([.. new[] { linked, unlinked, episodeLinked }.Select(id => Mock.Of<ISeries>(series => series.ID == id))]);
        var movieStore = new Mock<IMetadataMovieStore>();
        movieStore.Setup(s => s.GetAllMovies(Source)).Returns([]);
        var collectionStore = new Mock<IMetadataCollectionStore>();
        var usedCollection = ID(MetadataEntityType.Collection, "used");
        var unusedCollection = ID(MetadataEntityType.Collection, "unused");
        collectionStore.Setup(s => s.GetAllCollections(Source)).Returns([Entity<ICollection>(usedCollection), Entity<ICollection>(unusedCollection)]);
        collectionStore.Setup(s => s.GetMembers(usedCollection)).Returns([unlinked, linked]);
        collectionStore.Setup(s => s.GetMembers(unusedCollection)).Returns([unlinked]);
        var action = new PurgeAllUnusedMetadataAction(
            Manager(infos).Object,
            harness.PurgeService(links, infos, seriesStore, movieStore, collectionStore)
        );

        await action.Execute(new Progress<decimal>(), TestContext.Current.CancellationToken);

        Assert.Equal([unlinked.ToString(), unusedCollection.ToString()], harness.Queued.Select(queued => ((PurgeMetadataJob)queued.Job).EntryID));
    }

    [Fact]
    public async Task TheLibraryRefreshActionRefreshesEveryLinkedEntryOnce()
    {
        var harness = new SchedulerHarness();
        var infos = new[] { Info(new FakeProvider()) };
        var links = Links(series: ["1"], movies: ["9"]);
        links.Setup(s => s.GetAllSeriesLinks(Source)).Returns([
            new CrossRef_AniDB_Metadata_Series { Source = Source, AnidbAnimeID = AnimeID, ProviderID = "1" },
            new CrossRef_AniDB_Metadata_Series { Source = Source, AnidbAnimeID = AnimeID + 1, ProviderID = "1" },
        ]);
        links.Setup(s => s.GetAllMovieLinks(Source)).Returns([new CrossRef_AniDB_Metadata_Movie { Source = Source, AnidbAnimeID = AnimeID, AnidbEpisodeID = 1, ProviderID = "9" }]);
        var collections = new Mock<IMetadataCollectionStore>();
        var collectionID = ID(MetadataEntityType.Collection, "c");
        collections.Setup(s => s.GetAllCollections(Source)).Returns([Entity<ICollection>(collectionID)]);
        var action = new RefreshAllLinkedMetadataAction(Manager(infos).Object, harness.Service(links, infos, collections));

        await action.Execute(new Progress<decimal>(), TestContext.Current.CancellationToken);

        Assert.All(harness.Queued, queued => Assert.Equal(typeof(RefreshMetadataJob<FakeProvider>), queued.JobType));
        var jobs = harness.Queued.Select(queued => (IMetadataRefreshJob)queued.Job).ToList();
        Assert.Equal(
            [ID(MetadataEntityType.Series, "1").ToString(), ID(MetadataEntityType.Movie, "9").ToString(), collectionID.ToString()],
            jobs.Select(job => job.EntryID)
        );
        Assert.All(jobs.Take(2), job => Assert.Equal(AnimeID, job.AnimeID));
        Assert.All(jobs, job => Assert.False(job.Force));
        Assert.All(jobs, job => Assert.Equal(MetadataRefreshReason.Requested, job.Reason));
        Assert.All(jobs, job => Assert.False(job.AllowUnlinked));
    }

    [Fact]
    public async Task TheLibraryRefreshAndImagesLeaveOutAnimeNotInTheLibrary()
    {
        var harness = new SchedulerHarness();
        var infos = new[] { Info(new FakeProvider()) };
        var links = Links();
        const int Missing = AnimeID + 50;
        links.Setup(s => s.GetAllSeriesLinks(Source)).Returns([
            new CrossRef_AniDB_Metadata_Series { Source = Source, AnidbAnimeID = Missing, ProviderID = "1" },
            new CrossRef_AniDB_Metadata_Series { Source = Source, AnidbAnimeID = AnimeID, ProviderID = "2" },
            new CrossRef_AniDB_Metadata_Series { Source = Source, AnidbAnimeID = Missing, ProviderID = "3" },
            new CrossRef_AniDB_Metadata_Series { Source = Source, AnidbAnimeID = AnimeID, ProviderID = "3" },
        ]);
        links.Setup(s => s.GetAllMovieLinks(Source)).Returns([]);
        var service = harness.Service(links, infos, notInLibrary: [Missing]);

        Assert.Equal(2, await service.RefreshAllLinked(Source, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(2, await service.DownloadAllImages(Source, cancellationToken: TestContext.Current.CancellationToken));

        var refreshed = harness.Queued.Take(2).Select(queued => (IMetadataRefreshJob)queued.Job).ToList();
        Assert.Equal([ID(MetadataEntityType.Series, "2").ToString(), ID(MetadataEntityType.Series, "3").ToString()], refreshed.Select(job => job.EntryID));
        Assert.All(refreshed, job => Assert.Equal(AnimeID, job.AnimeID));
        Assert.Equal(
            [ID(MetadataEntityType.Series, "2").ToString(), ID(MetadataEntityType.Series, "3").ToString()],
            harness.Queued.Skip(2).Select(queued => ((IMetadataImagesJob)queued.Job).EntryID)
        );
    }

    [Fact]
    public async Task TheLibraryRefreshCanBeNarrowedToOneKind()
    {
        var harness = new SchedulerHarness();
        var infos = new[] { Info(new FakeProvider()) };
        var links = Links(series: ["1"], movies: ["9"]);
        links.Setup(s => s.GetAllSeriesLinks(Source)).Returns([new CrossRef_AniDB_Metadata_Series { Source = Source, AnidbAnimeID = AnimeID, ProviderID = "1" }]);
        links.Setup(s => s.GetAllMovieLinks(Source)).Returns([new CrossRef_AniDB_Metadata_Movie { Source = Source, AnidbAnimeID = AnimeID, AnidbEpisodeID = 1, ProviderID = "9" }]);
        var collections = new Mock<IMetadataCollectionStore>();
        collections.Setup(s => s.GetAllCollections(Source)).Returns([Entity<ICollection>(ID(MetadataEntityType.Collection, "c"))]);
        var service = harness.Service(links, infos, collections);

        Assert.Equal(1, await service.RefreshAllLinked(Source, entityType: MetadataEntityType.Movie, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal([ID(MetadataEntityType.Movie, "9").ToString()], harness.Queued.Select(queued => ((IMetadataRefreshJob)queued.Job).EntryID));
    }

    [Fact]
    public async Task TheImageActionsForceTheImagesOfWhatIsLinked()
    {
        var seriesHarness = new SchedulerHarness();
        var allHarness = new SchedulerHarness();
        var infos = new[] { Info(new FakeProvider()), Info(new SeriesOnlyProvider()), Info(new FakeTmdbProvider()) };
        var links = Links(series: ["1"], movies: ["9"]);
        links.Setup(s => s.GetAllSeriesLinks(Source)).Returns([new CrossRef_AniDB_Metadata_Series { Source = Source, AnidbAnimeID = AnimeID, ProviderID = "1" }]);
        links.Setup(s => s.GetAllMovieLinks(Source))
            .Returns([new CrossRef_AniDB_Metadata_Movie { Source = Source, AnidbAnimeID = AnimeID, AnidbEpisodeID = 1, ProviderID = "9" }]);
        var seriesAction = InSeries(new DownloadLinkedMetadataImagesSeriesAction(seriesHarness.Service(links, infos)), AnimeID);
        var allAction = new DownloadAllLinkedMetadataImagesAction(allHarness.Build(links, infos), allHarness.Service(links, infos));

        await seriesAction.Execute(TestContext.Current.CancellationToken);
        await allAction.Execute(new Progress<decimal>(), TestContext.Current.CancellationToken);

        foreach (var harness in new[] { seriesHarness, allHarness })
        {
            Assert.Equal(
                [ID(MetadataEntityType.Series, "1").ToString(), ID(MetadataEntityType.Movie, "9").ToString()],
                harness.Queued.Select(queued => ((IMetadataImagesJob)queued.Job).EntryID)
            );
            Assert.All(harness.Queued, queued => Assert.True(((IMetadataImagesJob)queued.Job).Force));
        }
    }

    #endregion
}

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Repositories.Cached.Metadata.Text;
using Shoko.Server.Scheduling.Jobs.Metadata;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers how the core fetches the collection a stored movie names through
/// the collection provider of its source: on save, and on a read that finds
/// it missing, only while the collection kind is on and the collection is due.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class MetadataCollectionRefreshTests
{
    #region Fixture

    private static readonly MetadataGuid s_movie = new(TestSources.Plugin, MetadataEntityType.Movie, "m1");

    private static readonly MetadataGuid s_collection = new(TestSources.Plugin, MetadataEntityType.Collection, "c1");

    /// <summary>
    /// A provider of movies and the collections they are in.
    /// </summary>
    public sealed class CollectionProvider : IMetadataMovieProvider, IMetadataCollectionProvider
    {
        public string Name => "Collections";

        public MetadataSource Source => TestSources.Plugin;

        public Task RefreshMovie(MetadataGuid movieID, MetadataRefreshOptions options, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RefreshCollection(MetadataGuid collectionID, MetadataRefreshOptions options, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class World
    {
        public Metadata_MovieRepository Movies { get; }

        public Metadata_CollectionRepository Collections { get; }

        public TextCache Texts { get; } = new();

        public MetadataTextStore TextStore { get; }

        public Mock<IMetadataCrossReferenceStore> Links { get; } = NoLinks.Build();

        /// <summary>
        /// The collections a fetch was queued for.
        /// </summary>
        public List<string> Queued { get; } = [];

        public MetadataCollectionRefreshScheduler Scheduler { get; }

        public World(bool collectionsOn = true, Metadata_Collection[]? collections = null, Metadata_Movie[]? movies = null)
        {
            Movies = CachedRepo.Build<Metadata_MovieRepository, int, Metadata_Movie>(row => row.Metadata_MovieID, movies ?? []);
            Collections = CachedRepo.Build<Metadata_CollectionRepository, int, Metadata_Collection>(row => row.Metadata_CollectionID, collections ?? []);
            TextStore = new(Texts, new CacheOnlyRowWriter());
            Links.Setup(store => store.GetLinksTo(s_movie)).Returns([Mock.Of<IMetadataMovieCrossReference>()]);
            Links.Setup(store => store.GetEpisodeLinksInto(It.IsAny<MetadataGuid>())).Returns([]);

            var provider = new CollectionProvider();
            var info = new MetadataProviderInfo
            {
                ID = Guid.NewGuid(),
                Version = new(1, 0),
                Name = provider.Name,
                Description = string.Empty,
                Provider = provider,
                ConfigurationInfo = null,
                PluginInfo = null!,
                SupportsSeries = false,
                SupportsMovies = true,
                SupportsCollections = true,
                SupportsAutoLinking = false,
                Source = provider.Source,
                AvailableEntityTypes = new HashSet<MetadataEntityType> { MetadataEntityType.Movie, MetadataEntityType.Collection },
                EnabledEntityTypes = collectionsOn
                    ? new HashSet<MetadataEntityType> { MetadataEntityType.Movie, MetadataEntityType.Collection }
                    : new HashSet<MetadataEntityType> { MetadataEntityType.Movie },
            };
            var manager = new Mock<IMetadataProviderManager>();
            manager.SetupGet(m => m.MetadataProviders).Returns([info]);

            var queue = new Mock<IQueueScheduler>();
            queue.Setup(q => q.Enqueue(It.IsAny<Type>(), It.IsAny<Action<IQueueJob>?>(), It.IsAny<bool>()))
                .Returns((Type type, Action<IQueueJob>? configure, bool _) =>
                {
                    var job = (IQueueJob)RuntimeHelpers.GetUninitializedObject(type);
                    configure?.Invoke(job);
                    Queued.Add(((IMetadataRefreshJob)job).EntryID!);
                    return Task.CompletedTask;
                });
            Scheduler = new(
                manager.Object,
                queue.Object,
                Mock.Of<IJobFactory>(),
                Collections,
                Movies,
                new(() => Links.Object),
                NullLogger<MetadataCollectionRefreshScheduler>.Instance
            );
        }

        public MetadataMovieStore MovieStore()
            => new(
                Movies,
                CachedRepo.Build<Metadata_ContentRatingRepository, int, Metadata_ContentRating>(row => row.Metadata_ContentRatingID),
                TextStore,
                NoCleanup.Build(),
                Scheduler
            );

        public RepoFactoryScope Scope()
            => new RepoFactoryScope().Set(Movies).Set(Collections).Set(Texts).Set(TestTextManager.Build(TextStore));
    }

    private static Metadata_Collection StoredCollection(DateTime? lastRefreshedAt)
        => new() { Metadata_CollectionID = 1, Source = TestSources.Plugin, ProviderID = s_collection.ID, LastRefreshedAt = lastRefreshedAt };

    #endregion

    #region Saving

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ASavedMovieNamingAnUnknownCollectionQueuesItsFetchOnlyWhileTheCollectionKindIsOn(bool collectionsOn)
    {
        var world = new World(collectionsOn);
        using var scope = world.Scope();

        world.MovieStore().SaveMovie(new() { ID = s_movie, CollectionID = s_collection });

        Assert.Equal(collectionsOn ? [s_collection.ToString()] : [], world.Queued);
    }

    [Fact]
    public void AStoredCollectionIsFetchedAgainOnlyOnceItIsDue()
    {
        var fresh = new World(collections: [StoredCollection(DateTime.Now)]);
        using (fresh.Scope())
            fresh.MovieStore().SaveMovie(new() { ID = s_movie, CollectionID = s_collection });

        var stale = new World(collections: [StoredCollection(DateTime.Now.AddDays(-1))]);
        using (stale.Scope())
            stale.MovieStore().SaveMovie(new() { ID = s_movie, CollectionID = s_collection });

        Assert.Empty(fresh.Queued);
        Assert.Equal([s_collection.ToString()], stale.Queued);
    }

    [Fact]
    public void ACollectionOnlyAnUnlinkedMovieNamesIsNotFetched()
    {
        var world = new World();
        using var scope = world.Scope();
        world.Links.Setup(store => store.GetLinksTo(s_movie)).Returns([]);

        world.MovieStore().SaveMovie(new() { ID = s_movie, CollectionID = s_collection });

        Assert.Empty(world.Queued);
        Assert.False(world.Scheduler.IsWanted(s_collection));
    }

    #endregion

    #region Catching Up

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CatchingUpFetchesEveryMissingCollectionALinkedMovieNamesOnlyWhileTheKindIsOn(bool collectionsOn)
    {
        Metadata_Movie[] movies =
        [
            new() { Metadata_MovieID = 1, Source = TestSources.Plugin, ProviderID = s_movie.ID, ExtraData = new() { CollectionID = s_collection.ID } },
            new() { Metadata_MovieID = 2, Source = TestSources.Plugin, ProviderID = "m2", ExtraData = new() { CollectionID = s_collection.ID } },
            new() { Metadata_MovieID = 3, Source = TestSources.Plugin, ProviderID = "m3", ExtraData = new() { CollectionID = "c3" } },
        ];
        var stored = new Metadata_Collection { Metadata_CollectionID = 3, Source = TestSources.Plugin, ProviderID = "c3" };
        var world = new World(collectionsOn, collections: [stored], movies: movies);
        using var scope = world.Scope();

        var queued = await world.Scheduler.ScheduleAllMissing(cancellationToken: TestContext.Current.CancellationToken);

        // Once each, and a stored one is left to the library refresh.
        Assert.Equal(collectionsOn ? 1 : 0, queued);
        Assert.Equal(collectionsOn ? [s_collection.ToString()] : [], world.Queued);
    }

    #endregion

    #region Reading

    [Fact]
    public void AReadThatFindsANamedCollectionMissingQueuesItsFetch()
    {
        var movie = new Metadata_Movie
        {
            Metadata_MovieID = 1,
            Source = TestSources.Plugin,
            ProviderID = s_movie.ID,
            ExtraData = new() { CollectionID = s_collection.ID },
        };
        var world = new World(movies: [movie]);
        using var scope = world.Scope();
        var metadataService = new MetadataService(
            groupRepository: null!,
            seriesRepository: null!,
            episodeRepository: null!,
            videoRepository: null!,
            userRepository: null!,
            filterRepository: null!,
            channelRepository: null!,
            anidbSeriesRepository: null!,
            anidbEpisodeRepository: null!,
            anidbCreatorRepository: null!,
            anidbCharacterRepository: null!,
            anidbTagRepository: null!,
            seriesStore: null!,
            movieStore: null!,
            collectionStore: Mock.Of<IMetadataCollectionStore>(),
            peopleStore: null!,
            tagStore: null!,
            studioStore: null!,
            orderings: null!,
            customTagRepository: null!,
            xrefCustomTagRepository: null!,
            crossReferences: world.Links.Object,
            providerManager: new(() => Mock.Of<IMetadataProviderManager>()),
            logger: NullLogger<MetadataService>.Instance,
            collectionScheduler: new(() => world.Scheduler)
        );

        Assert.Null(metadataService.GetCollection(s_collection));
        Assert.Equal([s_collection.ToString()], world.Queued);
    }

    #endregion
}

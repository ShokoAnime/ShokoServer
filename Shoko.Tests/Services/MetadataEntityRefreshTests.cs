using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Workers;
using Shoko.Server.Actions;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Repositories.Cached.Metadata.Text;
using Shoko.Server.Repositories.Direct.Metadata;
using Shoko.Server.Scheduling.Acquisition.Filters;
using Shoko.Server.Scheduling.Jobs.Metadata;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers the stubs the people and studio stores keep for the creators,
/// characters, studios and networks a credit or link names before their source
/// saved them, and the routing of their refreshes to the provider taking their
/// source and kind: one job per due entry, the staleness window, the scheduled
/// action and the job itself.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class MetadataEntityRefreshTests
{
    #region Helpers

    private static readonly MetadataGuid _series = new(TestSources.Plugin, MetadataEntityType.Series, "1");

    private static readonly MetadataGuid _otherSeries = new(TestSources.Plugin, MetadataEntityType.Series, "2");

    private static MetadataGuid ID(MetadataEntityType kind, string id, MetadataSource? source = null)
        => new(source ?? TestSources.Plugin, kind, id);

    /// <summary>
    /// A provider refreshing its source's people, studios and networks one at
    /// a time, through a callback a test sets.
    /// </summary>
    public sealed class EntityProvider : IMetadataSeriesProvider, IMetadataEntityProvider, IMetadataImageProvider
    {
        public string Name => "Entities";

        public MetadataSource Source => TestSources.Plugin;

        public MetadataEntityScope EntityScope { get; set; } = MetadataEntityScope.ForSource(
            TestSources.Plugin,
            MetadataEntityType.Creator,
            MetadataEntityType.Character,
            MetadataEntityType.Studio,
            MetadataEntityType.Network
        );

        public TimeSpan? EntityStaleAfter { get; set; } = TimeSpan.FromDays(30);

        public TimeSpan? EntityMissRetryAfter { get; set; } = TimeSpan.FromDays(7);

        public Func<MetadataGuid, bool> Refresh { get; set; } = _ => false;

        public List<MetadataGuid> Asked { get; } = [];

        public Task<bool> RefreshEntity(MetadataGuid entityID, CancellationToken cancellationToken = default)
        {
            Asked.Add(entityID);
            return Task.FromResult(Refresh(entityID));
        }

        public Task RefreshSeries(MetadataGuid seriesID, MetadataRefreshOptions options, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<ImageCandidate>?> GetImages(MetadataGuid entityID, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ImageCandidate>?>([]);
    }

    private sealed class World : IDisposable
    {
        private readonly RepoFactoryScope _scope;

        public Metadata_CreatorRepository Creators { get; }
            = CachedRepo.Build<Metadata_CreatorRepository, int, Metadata_Creator>(row => row.Metadata_CreatorID);

        public Metadata_CharacterRepository Characters { get; }
            = CachedRepo.Build<Metadata_CharacterRepository, int, Metadata_Character>(row => row.Metadata_CharacterID);

        public Metadata_StudioRepository Studios { get; }
            = CachedRepo.Build<Metadata_StudioRepository, int, Metadata_Studio>(row => row.Metadata_StudioID);

        public Metadata_Studio_EntryRepository StudioEntries { get; }
            = CachedRepo.Build<Metadata_Studio_EntryRepository, int, Metadata_Studio_Entry>(row => row.Metadata_Studio_EntryID);

        public Metadata_NetworkRepository Networks { get; }
            = CachedRepo.Build<Metadata_NetworkRepository, int, Metadata_Network>(row => row.Metadata_NetworkID);

        public Metadata_Network_EntryRepository NetworkEntries { get; }
            = CachedRepo.Build<Metadata_Network_EntryRepository, int, Metadata_Network_Entry>(row => row.Metadata_Network_EntryID);

        public Metadata_CastRepository Cast { get; } = new InMemoryCastRepository();

        public Metadata_CrewRepository Crew { get; } = new InMemoryCrewRepository();

        public EntityProvider Provider { get; } = new();

        public MetadataProviderInfo Info { get; }

        public MetadataRefreshState RefreshState { get; }

        public List<(Type JobType, IMetadataEntityRefreshJob Job, bool Prioritized)> Queued { get; } = [];

        public List<(Type JobType, IQueueJob Job)> QueuedOther { get; } = [];

        public MetadataEntityRefreshScheduler Scheduler { get; }

        public MetadataPeopleStore People { get; }

        public MetadataStudioStore StudioStore { get; }

        public World(params MetadataEntityType[] enabled)
        {
            var writer = new CacheOnlyRowWriter();
            var textStore = new MetadataTextStore(new TextCache(), writer);
            Info = new()
            {
                ID = Guid.NewGuid(),
                Version = new(1, 0),
                Name = Provider.Name,
                Description = string.Empty,
                Provider = Provider,
                ConfigurationInfo = null,
                PluginInfo = null!,
                SupportsSeries = true,
                SupportsMovies = false,
                SupportsCollections = false,
                SupportsAutoLinking = false,
                Source = Provider.Source,
                AvailableEntityTypes = MetadataEntityRefreshScheduler.EntityKinds,
                EnabledEntityTypes = enabled.Length > 0 ? new HashSet<MetadataEntityType>(enabled) : MetadataEntityRefreshScheduler.EntityKinds,
            };
            var manager = new Mock<IMetadataProviderManager>();
            manager.SetupGet(m => m.MetadataProviders).Returns([Info]);
            manager.Setup(m => m.GetProviderInfo(It.IsAny<Type>())).Returns(Info);
            var queue = new Mock<IQueueScheduler>();
            queue.Setup(q => q.Enqueue(It.IsAny<Type>(), It.IsAny<Action<IQueueJob>?>(), It.IsAny<bool>()))
                .Returns((Type type, Action<IQueueJob>? configure, bool prioritize) =>
                {
                    var job = (IQueueJob)RuntimeHelpers.GetUninitializedObject(type);
                    configure?.Invoke(job);
                    if (job is IMetadataEntityRefreshJob refresh)
                        Queued.Add((type, refresh, prioritize));
                    else
                        QueuedOther.Add((type, job));
                    return Task.CompletedTask;
                });
            queue.Setup(q => q.EnqueueWithPriority(It.IsAny<Type>(), It.IsAny<Action<IQueueJob>?>(), It.IsAny<int>()))
                .Returns((Type type, Action<IQueueJob>? configure, int _) =>
                {
                    var job = (IQueueJob)RuntimeHelpers.GetUninitializedObject(type);
                    configure?.Invoke(job);
                    QueuedOther.Add((type, job));
                    return Task.CompletedTask;
                });
            Scheduler = new(manager.Object, queue.Object, Mock.Of<IJobFactory>(), Creators, Characters, Studios, Networks,
                NullLogger<MetadataEntityRefreshScheduler>.Instance);
            People = new(Creators, Characters, Cast, Crew, textStore, writer, Scheduler);
            StudioStore = new(Studios, StudioEntries, Networks, NetworkEntries, writer, textStore, Scheduler);
            RefreshState = new(Mock.Of<IMetadataService>(), null!, null!, null!, People, StudioStore);
            _scope = new RepoFactoryScope().Set(Creators).Set(Characters).Set(Studios).Set(Networks);
            Manager = manager;
            Queue = queue;
        }

        public Mock<IMetadataProviderManager> Manager { get; }

        public Mock<IQueueScheduler> Queue { get; }

        public IReadOnlyList<MetadataGuid> QueuedIDs
            => [.. Queued.Select(queued => MetadataGuid.Parse(queued.Job.EntityID))];

        public IMetadataStubRow Row(MetadataGuid id)
            => Scheduler.GetRow(id) ?? throw new InvalidOperationException($"{id} is not stored.");

        public RefreshMetadataEntityJob<EntityProvider> Job(MetadataGuid id, bool force = false)
        {
            var cancellation = new Mock<IJobCancellationAccessor>();
            cancellation.SetupGet(accessor => accessor.Token).Returns(CancellationToken.None);
            var providerScheduler = new MetadataProviderScheduler(
                Manager.Object,
                Mock.Of<IMetadataCrossReferenceStore>(),
                Mock.Of<IMetadataRefreshState>(),
                Queue.Object,
                Mock.Of<IJobFactory>(),
                NullLogger<MetadataProviderScheduler>.Instance,
                Scheduler
            );
            var contributorScheduler = new MetadataImageContributorScheduler(
                Mock.Of<IMetadataImageContributorManager>(contributors => contributors.ImageContributors == new List<MetadataImageContributorInfo>()),
                Queue.Object,
                NullLogger<MetadataImageContributorScheduler>.Instance
            );
            var job = new RefreshMetadataEntityJob<EntityProvider>(
                Manager.Object,
                Scheduler,
                RefreshState,
                new MetadataEntryLocks(),
                providerScheduler,
                contributorScheduler,
                new StubSettingsProvider(new ServerSettings()),
                cancellation.Object
            )
            {
                EntityID = id.ToString(),
                Force = force,
            };
            job.Setup(new ServiceCollection().AddLogging().BuildServiceProvider());
            return job;
        }

        public void Dispose()
            => _scope.Dispose();
    }

    private sealed class PausedEntityProvider : IMetadataProviderPauseState
    {
        public event EventHandler? PausedProvidersChanged { add { } remove { } }

        public IReadOnlyList<Type> GetPausedProviderTypes()
            => [typeof(EntityProvider)];
    }

    private static string Name(IMetadataStubRow row)
        => row switch
        {
            Metadata_Creator creator => creator.Name,
            Metadata_Character character => character.Name,
            Metadata_Studio studio => studio.Name,
            Metadata_Network network => network.Name,
            _ => throw new ArgumentException("Not a stub row.", nameof(row)),
        };

    private static MetadataCastData Role(string character, string? creator, string? characterName = null, string? creatorName = null)
        => new()
        {
            CharacterID = ID(MetadataEntityType.Character, character),
            CreatorID = creator is null ? null : ID(MetadataEntityType.Creator, creator),
            Name = $"Role {character}",
            CharacterName = characterName,
            CreatorName = creatorName,
        };

    #endregion

    #region Stubs

    [Fact]
    public void CreditsAndLinksNamingUnstoredEntriesStoreStubsWithTheNamesTheyCarried()
    {
        using var world = new World();

        world.People.SetCast(_series, [Role("x1", "c1", "Alice", "Kana"), Role("x2", "c1")]);
        world.People.SetCrew(_series, [new() { CreatorID = ID(MetadataEntityType.Creator, "c2"), CreatorName = "  ", Name = "Music" }]);
        world.StudioStore.SetStudios(_series, [new() { StudioID = ID(MetadataEntityType.Studio, "s1"), StudioName = "Sunrise" }]);
        world.StudioStore.SetNetworks(_series, [new MetadataEntryNetworkData { NetworkID = ID(MetadataEntityType.Network, "n1"), NetworkName = "Tokyo MX" }]);
        world.StudioStore.SetNetworks(_otherSeries, [ID(MetadataEntityType.Network, "n2")]);

        var stubs = new[] { "c1", "c2" }.Select(id => ID(MetadataEntityType.Creator, id))
            .Concat(new[] { "x1", "x2" }.Select(id => ID(MetadataEntityType.Character, id)))
            .Append(ID(MetadataEntityType.Studio, "s1"))
            .Concat(new[] { "n1", "n2" }.Select(id => ID(MetadataEntityType.Network, id)))
            .Select(world.Row)
            .ToList();
        Assert.All(stubs, stub => Assert.True(stub.IsStub));
        Assert.All(stubs, stub => Assert.Null(stub.LastOrphanedAt));
        Assert.Equal(["Kana", "", "Alice", "Role x2", "Sunrise", "Tokyo MX", ""], stubs.Select(Name));

        // Readers see a stub as it is, through the entry naming it too.
        Assert.Equal(["Kana", "Kana"], world.People.GetCast(_series).Select(cast => cast.Creator!.Name));
        Assert.True(MetadataModelBuilder.IsStub(world.StudioStore.GetStudios(_series)[0]));
        Assert.Equal("Tokyo MX", world.StudioStore.GetNetworks(_series)[0].Name);
    }

    [Fact]
    public void ASaveFillsAStubInOnItsOwnRow()
    {
        using var world = new World();
        world.People.SetCast(_series, [Role("x1", "c1")]);
        world.StudioStore.SetStudios(_series, [new() { StudioID = ID(MetadataEntityType.Studio, "s1") }]);
        world.StudioStore.SetNetworks(_series, [ID(MetadataEntityType.Network, "n1")]);
        var creatorRow = world.Creators.GetByProviderID(TestSources.Plugin, "c1")!.Metadata_CreatorID;

        world.People.SaveCreators([new() { ID = ID(MetadataEntityType.Creator, "c1"), Name = "Kana" }]);
        world.People.SaveCharacters([new() { ID = ID(MetadataEntityType.Character, "x1"), Name = "Alice" }]);
        world.StudioStore.SaveStudios([new() { ID = ID(MetadataEntityType.Studio, "s1"), Name = "Sunrise" }]);
        world.StudioStore.SaveNetworks([new() { ID = ID(MetadataEntityType.Network, "n1"), Name = "Tokyo MX" }]);

        var creator = world.Creators.GetByProviderID(TestSources.Plugin, "c1")!;
        Assert.Equal(creatorRow, creator.Metadata_CreatorID);
        Assert.False(creator.IsStub);
        Assert.False(world.Row(ID(MetadataEntityType.Character, "x1")).IsStub);
        Assert.False(world.Row(ID(MetadataEntityType.Studio, "s1")).IsStub);
        Assert.False(world.Row(ID(MetadataEntityType.Network, "n1")).IsStub);
        Assert.Equal("Kana", world.People.GetCast(_series)[0].Creator!.Name);
    }

    [Fact]
    public void ALaterCreditOrLinkNamesAStubStoredWithoutOne()
    {
        using var world = new World();
        var creator = ID(MetadataEntityType.Creator, "c1");
        var character = ID(MetadataEntityType.Character, "x1");
        var studio = ID(MetadataEntityType.Studio, "s1");
        var network = ID(MetadataEntityType.Network, "n1");
        var saved = ID(MetadataEntityType.Creator, "saved");
        world.People.SetCast(_series, [new() { CharacterID = character, CreatorID = creator, Name = "" }]);
        world.StudioStore.SetStudios(_series, [new() { StudioID = studio }]);
        world.StudioStore.SetNetworks(_series, [network]);
        world.People.SaveCreators([new() { ID = saved, Name = "" }]);

        world.People.SetCast(_otherSeries, [Role("x1", null, characterName: "  Alice  ")]);
        world.People.SetCrew(_otherSeries, [
            new() { CreatorID = creator, CreatorName = "  ", Name = "Music" },
            new() { CreatorID = creator, CreatorName = " Kana ", Name = "Sound" },
            new() { CreatorID = saved, CreatorName = "Nope", Name = "Art" },
        ]);
        world.StudioStore.SetStudios(_otherSeries, [new() { StudioID = studio, StudioName = "Sunrise" }]);
        world.StudioStore.SetNetworks(_otherSeries, [new MetadataEntryNetworkData { NetworkID = network, NetworkName = "Tokyo MX" }]);

        // A named stub keeps its name.
        world.People.SetCast(_otherSeries, [Role("x1", null, characterName: "Bob")]);

        Assert.Equal(["Kana", "Alice", "Sunrise", "Tokyo MX", ""], new[] { creator, character, studio, network, saved }.Select(world.Row).Select(Name));
        Assert.True(world.Row(creator).IsStub);
    }

    [Fact]
    public void ThePurgeKeepsLinkedStubsAndDropsUnlinkedOnes()
    {
        using var world = new World();
        world.People.SetCast(_series, [Role("x1", "c1")]);
        world.People.SetCast(_otherSeries, [Role("x2", "c2")]);
        world.StudioStore.SetStudios(_series, [new() { StudioID = ID(MetadataEntityType.Studio, "s1") }]);
        world.StudioStore.SetStudios(_otherSeries, [new() { StudioID = ID(MetadataEntityType.Studio, "s2") }]);
        world.StudioStore.SetNetworks(_series, [ID(MetadataEntityType.Network, "n1")]);
        world.StudioStore.SetNetworks(_otherSeries, [ID(MetadataEntityType.Network, "n2")]);

        world.People.RemoveCast(_otherSeries);
        world.StudioStore.RemoveStudios(_otherSeries);
        world.StudioStore.RemoveNetworks(_otherSeries);
        var people = world.People.RemoveOrphaned(TestSources.Plugin, DateTime.MaxValue);
        var organisations = world.StudioStore.RemoveOrphaned(TestSources.Plugin, DateTime.MaxValue);

        Assert.Equal([ID(MetadataEntityType.Creator, "c2"), ID(MetadataEntityType.Character, "x2")], people);
        Assert.Equal([ID(MetadataEntityType.Studio, "s2"), ID(MetadataEntityType.Network, "n2")], organisations);
        Assert.True(world.Row(ID(MetadataEntityType.Creator, "c1")).IsStub);
        Assert.True(world.Row(ID(MetadataEntityType.Studio, "s1")).IsStub);
        Assert.True(world.Row(ID(MetadataEntityType.Network, "n1")).IsStub);
    }

    #endregion

    #region Routing

    [Fact]
    public void AWriteQueuesOneRefreshPerDueEntryFromTheProviderTakingItsKind()
    {
        using var world = new World();
        world.People.SaveCreators([new() { ID = ID(MetadataEntityType.Creator, "fresh"), Name = "Fresh" }]);

        world.People.SetCast(_series, [Role("x1", "c1"), Role("x2", "c1"), Role("x3", "fresh")]);

        Assert.All(world.Queued, queued => Assert.Equal(typeof(RefreshMetadataEntityJob<EntityProvider>), queued.JobType));
        Assert.Equal(
            [
                ID(MetadataEntityType.Creator, "c1"),
                ID(MetadataEntityType.Character, "x1"),
                ID(MetadataEntityType.Character, "x2"),
                ID(MetadataEntityType.Character, "x3"),
            ],
            world.QueuedIDs
        );

        // Another series naming the same creator queues it again under the same key, which the queue merges.
        world.People.SetCrew(_otherSeries, [new() { CreatorID = ID(MetadataEntityType.Creator, "c1"), Name = "Music" }]);
        var keys = world.Queued
            .Where(queued => queued.Job.EntityID == ID(MetadataEntityType.Creator, "c1").ToString())
            .Select(queued => JobKeyBuilder.BuildFor((IQueueJob)queued.Job))
            .ToList();
        Assert.Equal(2, keys.Count);
        Assert.Single(keys.Distinct());
        Assert.NotEqual(keys[0], JobKeyBuilder.BuildFor((IQueueJob)world.Queued[1].Job));
    }

    [Fact]
    public void AnEntryIsDueAsAStubOrOnceStaleAndAStaleOneARefreshLeftWaitsOutTheWindow()
    {
        using var world = new World();
        var now = DateTime.Now;
        var window = world.Provider.EntityStaleAfter!.Value;
        var stub = new Metadata_Creator { Source = TestSources.Plugin, ProviderID = "stub" };
        var fresh = new Metadata_Creator { Source = TestSources.Plugin, ProviderID = "fresh", LastUpdatedAt = now - window / 2 };
        var stale = new Metadata_Creator { Source = TestSources.Plugin, ProviderID = "stale", LastUpdatedAt = now - window * 2 };

        Assert.True(world.Scheduler.IsDue(stub, world.Provider, now));
        Assert.False(world.Scheduler.IsDue(fresh, world.Provider, now));
        Assert.True(world.Scheduler.IsDue(stale, world.Provider, now));

        // A refresh that left it as it was holds it back for the window.
        stale.LastRefreshedAt = now - window / 2;
        Assert.False(world.Scheduler.IsDue(stale, world.Provider, now));
        Assert.True(world.Scheduler.IsDue(stale, world.Provider, now + window));

        // Without a window a stale entry is never due again.
        world.Provider.EntityStaleAfter = null;
        Assert.False(world.Scheduler.IsDue(stale, world.Provider, now + window * 10));
    }

    [Fact]
    public void AStubARefreshMissedIsDueAgainAfterTheMissWindow()
    {
        using var world = new World();
        var now = DateTime.Now;
        var miss = world.Provider.EntityMissRetryAfter!.Value;
        var window = world.Provider.EntityStaleAfter!.Value;
        var stub = new Metadata_Creator { Source = TestSources.Plugin, ProviderID = "stub", LastRefreshedAt = now };

        Assert.False(world.Scheduler.IsDue(stub, world.Provider, now + miss / 2));
        Assert.True(world.Scheduler.IsDue(stub, world.Provider, now + miss));

        // Misses retry without a staleness window too.
        world.Provider.EntityStaleAfter = null;
        Assert.True(world.Scheduler.IsDue(stub, world.Provider, now + miss));

        // Without a miss window they wait out the staleness window, or for good without one.
        world.Provider.EntityMissRetryAfter = null;
        Assert.False(world.Scheduler.IsDue(stub, world.Provider, now + window * 10));
        world.Provider.EntityStaleAfter = window;
        Assert.False(world.Scheduler.IsDue(stub, world.Provider, now + miss));
        Assert.True(world.Scheduler.IsDue(stub, world.Provider, now + window));
    }

    [Fact]
    public void ASourceWithNoProviderOrAKindTurnedOffKeepsItsStubs()
    {
        using var world = new World(MetadataEntityType.Creator);
        var elsewhere = new MetadataGuid(TestSources.AniList, MetadataEntityType.Series, "1");

        world.People.SetCrew(elsewhere, [new() { CreatorID = ID(MetadataEntityType.Creator, "c1", TestSources.AniList), Name = "Music" }]);
        world.StudioStore.SetStudios(_series, [new() { StudioID = ID(MetadataEntityType.Studio, "s1") }]);

        Assert.Empty(world.Queued);
        Assert.True(world.Row(ID(MetadataEntityType.Creator, "c1", TestSources.AniList)).IsStub);
        Assert.True(world.Row(ID(MetadataEntityType.Studio, "s1")).IsStub);
        Assert.Null(world.Scheduler.GetProvider(TestSources.AniList, MetadataEntityType.Creator));
        Assert.Null(world.Scheduler.GetProvider(TestSources.Plugin, MetadataEntityType.Series));
    }

    [Fact]
    public void AScopeKeepsOnlyThePeopleStudiosAndNetworksOfTheProvidersOwnSource()
    {
        var provider = new EntityProvider
        {
            EntityScope = MetadataEntityScope.FromPairs([
                (TestSources.Plugin, MetadataEntityType.Creator),
                (TestSources.Plugin, MetadataEntityType.Series),
                (TestSources.AniList, MetadataEntityType.Studio),
            ]),
        };

        var (kinds, dropped) = MetadataEntityRefreshScheduler.GetKinds(provider);

        Assert.Equal([MetadataEntityType.Creator], kinds.ToArray());
        Assert.Equal(2, dropped.Count);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TheScheduledActionQueuesEveryLinkedStubOrStaleEntryOfTheKindsTurnedOn(bool networksOn)
    {
        using var world = networksOn
            ? new World()
            : new World(MetadataEntityType.Creator, MetadataEntityType.Character, MetadataEntityType.Studio);
        world.People.SetCast(_series, [Role("x1", "c1")]);
        world.People.SetCast(_otherSeries, [Role("x2", "gone")]);
        world.People.RemoveCast(_otherSeries);
        world.People.SaveCreators([new() { ID = ID(MetadataEntityType.Creator, "c1"), Name = "Kana" }]);
        world.StudioStore.SetNetworks(_series, [ID(MetadataEntityType.Network, "n1")]);
        world.Queued.Clear();
        var collections = new MetadataCollectionRefreshScheduler(
            world.Manager.Object,
            world.Queue.Object,
            Mock.Of<IJobFactory>(),
            CachedRepo.Build<Metadata_CollectionRepository, int, Metadata_Collection>(row => row.Metadata_CollectionID),
            CachedRepo.Build<Metadata_MovieRepository, int, Metadata_Movie>(row => row.Metadata_MovieID),
            new(() => NoLinks.Build().Object),
            NullLogger<MetadataCollectionRefreshScheduler>.Instance
        );

        var action = new RefreshStaleMetadataEntitiesAction(world.Scheduler, collections);
        await action.Execute(new Progress<decimal>(), TestContext.Current.CancellationToken);

        // The creator was refreshed, and nothing names the orphaned one any more.
        Assert.Equal(
            networksOn ? [ID(MetadataEntityType.Character, "x1"), ID(MetadataEntityType.Network, "n1")] : [ID(MetadataEntityType.Character, "x1")],
            world.QueuedIDs.OrderBy(id => id.ToString())
        );
        Assert.All(world.Queued, queued => Assert.False(queued.Job.Force));
    }

    [Fact]
    public async Task ARefreshAskedForByIDGoesToTheEntityJob()
    {
        using var world = new World();
        var scheduler = new MetadataProviderScheduler(
            world.Manager.Object,
            Mock.Of<IMetadataCrossReferenceStore>(),
            Mock.Of<IMetadataRefreshState>(),
            Mock.Of<IQueueScheduler>(),
            Mock.Of<IJobFactory>(),
            NullLogger<MetadataProviderScheduler>.Instance,
            world.Scheduler
        );
        var token = TestContext.Current.CancellationToken;
        var creator = ID(MetadataEntityType.Creator, "c1");

        Assert.True(await scheduler.ScheduleRefreshForEntry(creator, force: true, cancellationToken: token));
        Assert.False(await scheduler.ScheduleRefreshForEntry(ID(MetadataEntityType.Creator, "c1", TestSources.AniList), cancellationToken: token));

        var (jobType, job, prioritized) = Assert.Single(world.Queued);
        Assert.Equal(typeof(RefreshMetadataEntityJob<EntityProvider>), jobType);
        Assert.Equal(creator.ToString(), job.EntityID);
        Assert.True(job.Force);
        Assert.True(prioritized);
    }

    #endregion

    #region Job

    [Fact]
    public async Task TheJobRefreshesADueEntryAndStampsEveryAttempt()
    {
        using var world = new World();
        world.People.SetCast(_series, [Role("x1", "c1")]);
        var creator = ID(MetadataEntityType.Creator, "c1");
        var character = ID(MetadataEntityType.Character, "x1");
        world.Provider.Refresh = id =>
        {
            if (id != creator)
                return false;

            world.People.SaveCreators([new() { ID = id, Name = "Kana" }]);
            return true;
        };

        await world.Job(creator).Execute();
        await world.Job(character).Execute();

        // Found or not, the attempt is stamped on the row.
        Assert.False(world.Row(creator).IsStub);
        Assert.NotNull(world.Row(creator).LastRefreshedAt);
        Assert.True(world.Row(character).IsStub);
        Assert.NotNull(world.Row(character).LastRefreshedAt);

        // Neither is due now, so only a forced refresh asks again.
        await world.Job(creator).Execute();
        await world.Job(character).Execute();
        Assert.Equal([creator, character], world.Provider.Asked);
        await world.Job(character, force: true).Execute();
        Assert.Equal([creator, character, character], world.Provider.Asked);
    }

    [Fact]
    public async Task AnEntryFoundHasItsImagesQueuedAndAMissDoesNot()
    {
        using var world = new World();
        world.People.SetCast(_series, [Role("x1", "c1")]);
        var creator = ID(MetadataEntityType.Creator, "c1");
        var character = ID(MetadataEntityType.Character, "x1");
        world.Provider.Refresh = id =>
        {
            if (id != creator)
                return false;

            world.People.SaveCreators([new() { ID = id, Name = "Kana" }]);
            return true;
        };

        await world.Job(creator).Execute();
        await world.Job(character).Execute();

        var (jobType, job) = Assert.Single(world.QueuedOther);
        Assert.Equal(typeof(DownloadMetadataImagesJob<EntityProvider>), jobType);
        Assert.Equal(creator.ToString(), ((IMetadataImagesJob)job).EntryID);

        // The image job walks the entry alone.
        var stored = world.Creators.GetByProviderID(TestSources.Plugin, "c1")!;
        var (entities, _) = MetadataImageEntities.Gather(Mock.Of<IMetadataService>(service => service.GetEntry(creator) == stored), creator);
        Assert.Same(stored, Assert.Single(entities));
    }

    [Fact]
    public async Task TheJobLeavesAKindTheProviderIsNotEnabledFor()
    {
        using var world = new World(MetadataEntityType.Creator);
        world.StudioStore.SetStudios(_series, [new() { StudioID = ID(MetadataEntityType.Studio, "s1") }]);

        await world.Job(ID(MetadataEntityType.Studio, "s1"), force: true).Execute();

        Assert.Empty(world.Provider.Asked);
    }

    [Fact]
    public void AnEntityProvidersRefreshesAreItsOwnJobTypeAndWaitOutItsPause()
    {
        var jobType = typeof(RefreshMetadataEntityJob<EntityProvider>);
        Assert.Contains(jobType, MetadataProviderJobs.GetJobTypes(typeof(EntityProvider)));
        Assert.Equal(jobType, MetadataProviderJobs.GetEntityRefreshJobType(typeof(EntityProvider)));
        Assert.Equal(typeof(EntityProvider), MetadataProviderJobs.GetProviderType(jobType));

        using var filter = new MetadataProviderPausedAcquisitionFilter(new PausedEntityProvider(), MetadataProviderJobs.GetJobTypes(typeof(EntityProvider)));
        Assert.Contains(jobType, filter.GetTypesToExclude());
    }

    #endregion
}

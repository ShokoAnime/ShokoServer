using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ImageMagick;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Image.Options;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Workers;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Scheduling.Jobs.Metadata;
using Shoko.Server.Services;
using Shoko.Server.Services.Configuration;
using Shoko.Server.Settings;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Scheduling.Jobs.Metadata;

/// <summary>
/// Covers the image contributors: how they are registered and turned on and
/// off, which of them an entry's image refresh queues, what their job does,
/// and how the links of a pair turned off are removed.
/// </summary>
public sealed class MetadataImageContributorTests : IDisposable
{
    #region Fixtures

    private static readonly MetadataGuid ShowID = new(MetadataSource.TMDB, MetadataEntityType.Series, "1");

    private static readonly MetadataGuid EpisodeID = new(MetadataSource.TMDB, MetadataEntityType.Episode, "11");

    private static readonly MetadataGuid MovieID = new(MetadataSource.TMDB, MetadataEntityType.Movie, "9");

    private readonly string _dataPath = Path.Join(Path.GetTempPath(), $"shoko-image-contributor-tests-{Guid.NewGuid():N}");

    public MetadataImageContributorTests()
        => Directory.CreateDirectory(_dataPath);

    public void Dispose()
    {
        if (Directory.Exists(_dataPath))
            Directory.Delete(_dataPath, recursive: true);
    }

    /// <summary>
    /// A contributor of artwork for TMDB's shows, episodes and movies, which
    /// records what it was asked about.
    /// </summary>
    public sealed class ArtContributor : IMetadataImageContributor
    {
        public string Name => "Art";

        public MetadataSource Source => TestSources.Plugin;

        public MetadataEntityScope Scope { get; init; } = MetadataEntityScope.ForSource(
            MetadataSource.TMDB,
            MetadataEntityType.Series,
            MetadataEntityType.Episode,
            MetadataEntityType.Movie
        );

        public int? MaxConcurrentJobs => 3;

        public List<IMetadata> Asked { get; } = [];

        public Dictionary<MetadataGuid, IReadOnlyList<ImageCandidate>> Images { get; } = [];

        public Task<IReadOnlyList<ImageCandidate>?> GetImages(IMetadata entity, CancellationToken cancellationToken = default)
        {
            Asked.Add(entity);
            return Task.FromResult<IReadOnlyList<ImageCandidate>?>(Images.GetValueOrDefault(entity.ID));
        }
    }

    /// <summary>
    /// A contributor of AniDB anime posters, with no limit of its own.
    /// </summary>
    public sealed class PosterContributor : IMetadataImageContributor
    {
        public string Name => "Posters";

        public MetadataSource Source { get; init; } = TestSources.AniList;

        public MetadataEntityScope Scope { get; init; } = MetadataEntityScope.Single(MetadataSource.AniDB, MetadataEntityType.Series);

        public Task<IReadOnlyList<ImageCandidate>?> GetImages(IMetadata entity, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ImageCandidate>?>(null);
    }

    /// <summary>
    /// A contributor naming a core source as its own.
    /// </summary>
    public sealed class CoreSourceContributor : IMetadataImageContributor
    {
        public string Name => "Core";

        public MetadataSource Source => MetadataSource.AniDB;

        public MetadataEntityScope Scope => MetadataEntityScope.Single(MetadataSource.AniDB, MetadataEntityType.Series);

        public Task<IReadOnlyList<ImageCandidate>?> GetImages(IMetadata entity, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ImageCandidate>?>(null);
    }

    private sealed class Harness
    {
        public List<(Type JobType, IQueueJob Job, bool Prioritized)> Queued { get; } = [];

        public Mock<IQueueScheduler> Queue { get; } = new();

        public ConfigurationService Configuration { get; }

        public MetadataImageContributorManager Manager { get; }

        public Harness(string dataPath, params IMetadataImageContributor[] contributors)
            : this(dataPath, PluginTestDoubles.InstalledPluginInfo(typeof(PluginTestDoubles.TestPlugin), Guid.Parse("33333333-3333-3333-3333-333333333333")), contributors)
        {
        }

        public Harness(string dataPath, LocalPluginInfo pluginInfo, params IMetadataImageContributor[] contributors)
        {
            // The real service keeps the file, so a new harness on the same
            // path reads back what an earlier one saved, as a restart would.
            var applicationPaths = new Mock<IApplicationPaths>(MockBehavior.Loose);
            applicationPaths.SetupGet(paths => paths.DataPath).Returns(dataPath);
            applicationPaths.SetupGet(paths => paths.ConfigurationsPath).Returns(Path.Join(dataPath, "configurations"));
            applicationPaths.SetupGet(paths => paths.PluginsPath).Returns(Path.Join(dataPath, "plugins"));
            applicationPaths.SetupGet(paths => paths.ApplicationPath).Returns(Path.Join(dataPath, "app"));
            Configuration = new ConfigurationService(NullLoggerFactory.Instance, applicationPaths.Object, new Mock<IPluginManager>(MockBehavior.Loose).Object);
            var configurationInfo = (ConfigurationInfo)RuntimeHelpers.GetUninitializedObject(typeof(ConfigurationInfo));
            var configurationService = new Mock<IConfigurationService>();
            configurationService.Setup(service => service.GetConfigurationInfo<MetadataServiceSettings>()).Returns(configurationInfo);
            configurationService.Setup(service => service.Load(It.IsAny<ConfigurationInfo>(), It.IsAny<bool>()))
                .Returns(() => Configuration.Load<MetadataServiceSettings>());
            configurationService.Setup(service => service.Save(It.IsAny<MetadataServiceSettings>()))
                .Returns((MetadataServiceSettings settings) => Configuration.Save(settings));

            var pluginManager = new Mock<IPluginManager>();
            pluginManager.Setup(manager => manager.GetPluginInfo(It.IsAny<Assembly>())).Returns(pluginInfo);
            Queue.Setup(q => q.Enqueue(It.IsAny<Type>(), It.IsAny<Action<IQueueJob>?>(), It.IsAny<bool>()))
                .Returns((Type type, Action<IQueueJob>? configure, bool prioritize) => Record(type, configure, prioritize));
            Queue.Setup(q => q.Enqueue(It.IsAny<Action<ClearContributedImagesJob>?>(), It.IsAny<bool>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
                .Returns((Action<ClearContributedImagesJob>? configure, bool prioritize, DateTimeOffset? _, CancellationToken _) =>
                    Record(typeof(ClearContributedImagesJob), job => configure?.Invoke((ClearContributedImagesJob)job), prioritize));
            Manager = new(
                pluginManager.Object,
                new ConfigurationProvider<MetadataServiceSettings>(configurationService.Object),
                Queue.Object,
                applicationPaths.Object,
                NullLogger<MetadataImageContributorManager>.Instance
            );
            Manager.AddParts(contributors);
        }

        public MetadataImageContributorScheduler Scheduler()
            => new(Manager, Queue.Object, NullLogger<MetadataImageContributorScheduler>.Instance);

        private Task Record(Type type, Action<IQueueJob>? configure, bool prioritize)
        {
            var job = (IQueueJob)RuntimeHelpers.GetUninitializedObject(type);
            configure?.Invoke(job);
            Queued.Add((type, job, prioritize));
            return Task.CompletedTask;
        }
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

    private static T Entity<T>(MetadataGuid id) where T : class, IMetadata
    {
        var entity = new Mock<T>();
        entity.SetupGet(e => e.ID).Returns(id);
        return entity.Object;
    }

    #endregion

    #region Registration

    [Fact]
    public void AContributorIsRefusedForAnUnregisteredCoreLocalOrTakenSource_AndLosesThePairsOnItsOwnSource()
    {
        var art = new ArtContributor();
        var clash = new PosterContributor { Source = TestSources.Plugin };
        var own = new PosterContributor
        {
            Scope = MetadataEntityScope.FromPairs([(MetadataSource.AniDB, MetadataEntityType.Series), (TestSources.AniList, MetadataEntityType.Series)]),
        };
        var local = new PosterContributor { Source = TestSources.LocalPlugin };
        var unregistered = new PosterContributor { Source = MetadataSource.GetByValue("unregistered-art") };
        var harness = new Harness(_dataPath, art, new CoreSourceContributor(), clash, local, unregistered, own);

        Assert.Equal([art, own], harness.Manager.ImageContributors.Select(info => info.Contributor).ToList());
        var ownInfo = harness.Manager.GetImageContributorInfo(own);
        Assert.Equal(MetadataEntityScope.Single(MetadataSource.AniDB, MetadataEntityType.Series), ownInfo.AvailableScope);

        // Every pair starts on.
        var artInfo = harness.Manager.GetImageContributorInfo(art);
        Assert.Equal(art.Scope, artInfo.EnabledScope);
        Assert.Equal(3, artInfo.MaxConcurrentJobs);
        Assert.Equal(MetadataImageContributorManager.DefaultMaxConcurrentJobs, ownInfo.MaxConcurrentJobs);
        Assert.Equal([artInfo], harness.Manager.GetImageContributorsFor(EpisodeID));
    }

    [Fact]
    public void AContributorTakesTheIconBesideItsPluginNamedAfterItsSource()
    {
        var directory = Directory.CreateDirectory(Path.Join(_dataPath, "plugins", "Art")).FullName;
        using (var image = new MagickImage(MagickColors.Red, 2, 2))
            image.Write(Path.Join(directory, MetadataImageContributorManager.IconKind(TestSources.Plugin) + ".png"));
        var pluginInfo = PluginTestDoubles.InstalledPluginInfoInFolder(typeof(PluginTestDoubles.TestPlugin), Guid.NewGuid(), directory, Path.Join(directory, "Art.dll"));
        var art = new ArtContributor();
        var posters = new PosterContributor();

        var harness = new Harness(_dataPath, pluginInfo, art, posters);

        var icon = harness.Manager.GetImageContributorInfo(art).Icon;
        Assert.Equal(("%PluginsPath%/Art/test-plugin.images-icon.png", "image/png"), (icon?.FilePath.Replace('\\', '/'), icon?.MimeType));
        Assert.Null(harness.Manager.GetImageContributorInfo(posters).Icon);
    }

    [Fact]
    public void ThePairsTurnedOffAreKeptAcrossARestart_AndTheirLinksAreQueuedForRemoval()
    {
        var art = new ArtContributor();
        var harness = new Harness(_dataPath, art);
        var info = harness.Manager.GetImageContributorInfo(art);

        harness.Manager.SetImageContributorEnabled(info.ID, MetadataEntityScope.Single(MetadataSource.TMDB, MetadataEntityType.Movie));

        Assert.Equal(MetadataEntityScope.Single(MetadataSource.TMDB, MetadataEntityType.Movie), info.EnabledScope);
        var clear = Assert.Single(harness.Queued);
        Assert.Equal(typeof(ClearContributedImagesJob), clear.JobType);
        Assert.Equal(info.ID, ((ClearContributedImagesJob)clear.Job).ContributorID);
        Assert.Equal(TestSources.Plugin.Value, ((ClearContributedImagesJob)clear.Job).Source);

        // A new server reads the same decisions back.
        var restarted = new Harness(_dataPath, new ArtContributor());
        Assert.Equal(MetadataEntityScope.Single(MetadataSource.TMDB, MetadataEntityType.Movie), restarted.Manager.ImageContributors[0].EnabledScope);

        // Turning everything back on removes nothing and forgets the decision.
        restarted.Manager.SetImageContributorEnabled(info.ID, restarted.Manager.ImageContributors[0].AvailableScope);
        Assert.Empty(restarted.Queued);
        Assert.Empty(restarted.Configuration.Load<MetadataServiceSettings>().ImageContributors);
        Assert.Equal(art.Scope, new Harness(_dataPath, new ArtContributor()).Manager.ImageContributors[0].EnabledScope);
    }

    [Fact]
    public void APairTheContributorCannotServeIsRefused()
    {
        var harness = new Harness(_dataPath, new ArtContributor());
        var info = harness.Manager.ImageContributors[0];

        Assert.Throws<ArgumentException>(() => harness.Manager.SetImageContributorEnabled(info.ID, MetadataEntityScope.Single(MetadataSource.AniDB, MetadataEntityType.Series)));
        Assert.Throws<ArgumentException>(() => harness.Manager.SetImageContributorEnabled(Guid.NewGuid(), MetadataEntityScope.Empty));
        Assert.Equal(info.AvailableScope, info.EnabledScope);
    }

    #endregion

    #region Jobs and Pools

    [Fact]
    public void EachContributorGetsAJobTypeAndAPoolOfItsOwn()
    {
        Assert.Equal(typeof(DownloadContributedImagesJob<ArtContributor>), MetadataImageContributorJobs.GetJobType(typeof(ArtContributor)));
        Assert.Equal(typeof(ArtContributor), MetadataImageContributorJobs.GetContributorType(typeof(DownloadContributedImagesJob<ArtContributor>)));
        Assert.Null(MetadataImageContributorJobs.GetJobType(typeof(string)));
        Assert.Null(MetadataImageContributorJobs.GetJobType(typeof(IMetadataImageContributor)));
        Assert.Null(MetadataImageContributorJobs.GetContributorType(typeof(ClearContributedImagesJob)));

        var harness = new Harness(_dataPath, new ArtContributor(), new PosterContributor());
        var services = new ServiceCollection();
        services.AddSingleton<IMetadataImageContributorManager>(harness.Manager);
        var concurrency = new MetadataImageContributorJobConcurrency(services.BuildServiceProvider());

        Assert.Equal(3, concurrency.GetConcurrencyLimit(typeof(DownloadContributedImagesJob<ArtContributor>)));
        Assert.Equal(MetadataImageContributorManager.DefaultMaxConcurrentJobs, concurrency.GetConcurrencyLimit(typeof(DownloadContributedImagesJob<PosterContributor>)));
        Assert.Null(concurrency.GetConcurrencyLimit(typeof(ClearContributedImagesJob)));
    }

    #endregion

    #region Queueing

    [Fact]
    public async Task OnlyTheContributorsEnabledForTheEntryOrWhatIsUnderItAreQueued()
    {
        var art = new ArtContributor();
        var posters = new PosterContributor();
        var harness = new Harness(_dataPath, art, posters);
        var scheduler = harness.Scheduler();

        // A show reaches its episodes, so a contributor left on only for
        // episodes still covers it.
        harness.Manager.SetImageContributorEnabled(harness.Manager.GetImageContributorInfo(art).ID, MetadataEntityScope.Single(MetadataSource.TMDB, MetadataEntityType.Episode));
        harness.Queued.Clear();
        Assert.Equal(1, await scheduler.ScheduleForEntry(ShowID, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(typeof(DownloadContributedImagesJob<ArtContributor>), Assert.Single(harness.Queued).JobType);
        Assert.Equal(ShowID.ToString(), ((DownloadContributedImagesJob<ArtContributor>)harness.Queued[0].Job).EntryID);

        // A movie is not under anything left on.
        Assert.Equal(0, await scheduler.ScheduleForEntry(MovieID, cancellationToken: TestContext.Current.CancellationToken));

        // An AniDB anime goes to the poster contributor, forced and so ahead of the rest.
        harness.Queued.Clear();
        var anime = new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Series, "30");
        Assert.Equal(1, await scheduler.ScheduleForEntry(anime, force: true, cancellationToken: TestContext.Current.CancellationToken));
        var queued = Assert.Single(harness.Queued);
        Assert.Equal(typeof(DownloadContributedImagesJob<PosterContributor>), queued.JobType);
        Assert.True(queued.Prioritized);
        Assert.True(((DownloadContributedImagesJob<PosterContributor>)queued.Job).Force);
    }

    #endregion

    #region Execution

    private sealed class JobHarness
    {
        public Mock<IMetadataService> Metadata { get; } = new();

        public Mock<IImageManager> Images { get; } = new();

        public List<(IWithImages Entity, IImage Image, ImageCrossReferenceData Data)> Linked { get; } = [];

        public List<ImageData> Added { get; } = [];

        public ServerSettings Settings { get; } = new();

        public MetadataEntryLocks Locks { get; } = new();

        public JobHarness()
        {
            Images.Setup(i => i.GetTemplateUrlForSource(It.IsAny<MetadataSource>())).Returns("https://example.com/{0}");
            Images.Setup(i => i.GetImageCrossReferencesForEntity(It.IsAny<IWithImages>(), It.IsAny<ImageCrossReferenceFilteringOptions?>())).Returns([]);
            Images.Setup(i => i.AddImage(It.IsAny<ImageData>()))
                .Returns((ImageData data) =>
                {
                    Added.Add(data);
                    return Mock.Of<IImage>(image => image.ID == Guid.NewGuid() && image.Source == data.Source);
                });
            Images.Setup(i => i.AddImageCrossReference(It.IsAny<IWithImages>(), It.IsAny<IImage>(), It.IsAny<ImageCrossReferenceData>()))
                .Returns((IWithImages entity, IImage image, ImageCrossReferenceData data) =>
                {
                    Linked.Add((entity, image, data));
                    return Mock.Of<IImageCrossReference>();
                });
        }

        public DownloadContributedImagesJob<ArtContributor> Job(MetadataImageContributorManager manager, MetadataGuid entryID)
        {
            var job = Ready(new DownloadContributedImagesJob<ArtContributor>(
                manager,
                Metadata.Object,
                new MetadataImageReconciler(Images.Object, Locks, NullLogger<MetadataImageReconciler>.Instance),
                Locks,
                new StubSettingsProvider(Settings),
                NoCancellation()
            ));
            job.EntryID = entryID.ToString();
            return job;
        }
    }

    [Fact]
    public async Task TheJobResolvesTheEntryWhenItRuns_AndLinksTheImagesUnderTheContributor()
    {
        var art = new ArtContributor();
        var harness = new Harness(_dataPath, art);
        var jobs = new JobHarness();
        var episode = Entity<IEpisode>(EpisodeID);
        var season = Entity<ISeason>(new(MetadataSource.TMDB, MetadataEntityType.Season, "5"));
        var show = new Mock<ISeries>();
        show.SetupGet(s => s.ID).Returns(ShowID);
        show.SetupGet(s => s.Seasons).Returns([season]);
        show.SetupGet(s => s.Episodes).Returns([episode]);
        art.Images[ShowID] = [new() { ResourceID = "show-poster.jpg", ImageType = ImageEntityType.Primary }];

        // Queued while nothing stored the show: it is resolved when the job runs.
        var job = jobs.Job(harness.Manager, ShowID);
        jobs.Metadata.Setup(m => m.GetSeries(ShowID)).Returns(show.Object);
        await job.Execute();

        // The entity itself is handed over, and only the kinds in its scope.
        Assert.Equal([show.Object, episode], art.Asked);
        var added = Assert.Single(jobs.Added);
        Assert.Equal(TestSources.Plugin, added.Source);
        Assert.Equal("show-poster.jpg", added.ResourceID);
        var link = Assert.Single(jobs.Linked);
        Assert.Same(show.Object, link.Entity);
        Assert.Equal(TestSources.Plugin, link.Data.Source);
        jobs.Images.Verify(i => i.ScheduleAutoDownloadsForEntity(show.Object, TestSources.Plugin, null, TestSources.Plugin, false), Times.Once);
    }

    [Fact]
    public async Task TheJobSkipsQuietlyAnEntryThatIsGoneOrAKindTurnedOff()
    {
        var art = new ArtContributor();
        var harness = new Harness(_dataPath, art);
        var jobs = new JobHarness();

        // Gone.
        await jobs.Job(harness.Manager, MovieID).Execute();

        // Turned off.
        jobs.Metadata.Setup(m => m.GetMovie(MovieID)).Returns(Entity<IMovie>(MovieID));
        harness.Manager.SetImageContributorEnabled(harness.Manager.ImageContributors[0].ID, MetadataEntityScope.Single(MetadataSource.TMDB, MetadataEntityType.Series));
        await jobs.Job(harness.Manager, MovieID).Execute();

        Assert.Empty(art.Asked);
        Assert.Empty(jobs.Linked);
    }

    [Fact]
    public async Task ClearingRemovesOnlyTheLinksOfThePairsTurnedOff()
    {
        var art = new ArtContributor();
        var harness = new Harness(_dataPath, art);
        var info = harness.Manager.ImageContributors[0];
        harness.Manager.SetImageContributorEnabled(info.ID, MetadataEntityScope.Single(MetadataSource.TMDB, MetadataEntityType.Series));
        var onShow = Mock.Of<IImageCrossReference>(xref => xref.EntityID == ShowID);
        var onMovie = Mock.Of<IImageCrossReference>(xref => xref.EntityID == MovieID);
        var onOwnSource = Mock.Of<IImageCrossReference>(xref => xref.EntityID == new MetadataGuid(TestSources.Plugin, MetadataEntityType.Series, "1"));
        var images = new Mock<IImageManager>();
        images.Setup(i => i.GetAllImageCrossReferences(It.Is<ImageCrossReferenceFilteringOptions?>(options =>
                options != null && options.ImageSource == TestSources.Plugin && options.XrefSource == TestSources.Plugin)))
            .Returns([onShow, onMovie, onOwnSource]);
        images.Setup(i => i.RemoveImageCrossReference(It.IsAny<IImageCrossReference>())).Returns(true);
        var job = Ready(new ClearContributedImagesJob(harness.Manager, images.Object, NoCancellation())
        {
            ContributorID = info.ID,
            Source = info.Source.Value,
        });

        await job.Execute();

        images.Verify(i => i.RemoveImageCrossReference(onMovie), Times.Once);
        images.Verify(i => i.RemoveImageCrossReference(It.IsAny<IImageCrossReference>()), Times.Once);
    }

    #endregion
}

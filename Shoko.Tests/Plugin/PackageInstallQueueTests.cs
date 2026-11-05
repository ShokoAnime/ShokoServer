using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Exceptions;
using Shoko.Abstractions.Plugin.Models;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Plugin;
using Shoko.Server.Settings;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Plugin;

/// <summary>
///   Package installs run one at a time through the install queue, and asking
///   for a package already queued joins its install instead of racing it.
/// </summary>
public sealed class PackageInstallQueueTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("package-install-queue-").FullName;

    private readonly GatedArchiveHost _host = new();

    public PackageInstallQueueTests()
        => Directory.CreateDirectory(Path.Join(_root, "plugins"));

    public void Dispose()
        => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task TheSamePackageTwiceIsInstalledOnceForBothCallers()
    {
        var manager = CreatePackageManager();
        var plan = Plan(Package(Guid.NewGuid(), "First", "1.0.0"));

        var first = manager.InstallPackages(plan, TestContext.Current.CancellationToken);
        var second = manager.InstallPackages(plan, TestContext.Current.CancellationToken);

        var entry = Assert.Single(manager.GetInstallQueue());
        Assert.Equal(2, entry.Waiting);

        _host.Release();
        var results = await Task.WhenAll(first, second);

        Assert.Same(results[0], results[1]);
        Assert.Equal(1, _host.Requests);
        Assert.Empty(manager.GetInstallQueue());
    }

    [Fact]
    public async Task DifferentPackagesWaitTheirTurnInOrder()
    {
        var manager = CreatePackageManager();
        var first = manager.InstallPackages(Plan(Package(Guid.NewGuid(), "First", "1.0.0")), TestContext.Current.CancellationToken);
        var second = manager.InstallPackages(Plan(Package(Guid.NewGuid(), "Second", "1.0.0")), TestContext.Current.CancellationToken);

        var queue = await WaitForRunning(manager);
        Assert.Equal(["First", "Second"], queue.Select(entry => entry.Package.Manifest.Name));
        Assert.NotNull(queue[0].StartedAt);
        Assert.Null(queue[1].StartedAt);

        _host.Release();
        await Task.WhenAll(first, second);

        Assert.Equal(2, _host.Requests);
        Assert.Empty(manager.GetInstallQueue());
    }

    [Fact]
    public async Task QueuingWithoutWaitingCanBeWaitedForByID()
    {
        var manager = CreatePackageManager();
        var plan = Plan(Package(Guid.NewGuid(), "First", "1.0.0"));

        var entry = manager.QueueInstall(plan);
        Assert.Equal(0, entry.Waiting);
        Assert.Equal(entry.ID, manager.QueueInstall(plan).ID);
        Assert.Null(await manager.WaitForInstall(Guid.NewGuid(), TestContext.Current.CancellationToken));

        var waiting = manager.WaitForInstall(entry.ID, TestContext.Current.CancellationToken);
        Assert.Equal(1, Assert.Single(manager.GetInstallQueue()).Waiting);

        _host.Release();
        var installed = await waiting;

        Assert.NotNull(installed);
        Assert.Single(installed);
        Assert.Equal(1, _host.Requests);
    }

    [Fact]
    public async Task CancellingAWaitingEntryTakesItOffTheQueue()
    {
        var manager = CreatePackageManager();
        var first = manager.InstallPackages(Plan(Package(Guid.NewGuid(), "First", "1.0.0")), TestContext.Current.CancellationToken);
        var secondPlan = Plan(Package(Guid.NewGuid(), "Second", "1.0.0"));
        var second = manager.InstallPackages(secondPlan, TestContext.Current.CancellationToken);
        await WaitForRunning(manager);

        Assert.True(manager.CancelInstall(PluginPackageManager.GetInstallEntryID(secondPlan)));
        Assert.False(manager.CancelInstall(PluginPackageManager.GetInstallEntryID(secondPlan)));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);
        Assert.Equal(["First"], manager.GetInstallQueue().Select(entry => entry.Package.Manifest.Name));

        _host.Release();
        await first;

        Assert.Equal(1, _host.Requests);
        Assert.Empty(manager.GetInstallQueue());
    }

    [Fact]
    public async Task CancellingARunningEntryStopsItAndRunsTheNext()
    {
        var manager = CreatePackageManager();
        var firstPlan = Plan(Package(Guid.NewGuid(), "First", "1.0.0"));
        var first = manager.InstallPackages(firstPlan, TestContext.Current.CancellationToken);
        var second = manager.InstallPackages(Plan(Package(Guid.NewGuid(), "Second", "1.0.0")), TestContext.Current.CancellationToken);
        while (_host.Requests == 0)
            await Task.Delay(10, TestContext.Current.CancellationToken);

        Assert.True(manager.CancelInstall(PluginPackageManager.GetInstallEntryID(firstPlan)));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);

        _host.Release();
        await second;

        Assert.Equal(2, _host.Requests);
        Assert.Empty(manager.GetInstallQueue());
        Assert.False(Directory.Exists(Path.Join(_root, "plugins", "First-1.0.0-any")));
    }

    [Fact]
    public async Task APlanWithoutTheRoomForItIsRefusedBeforeDownloading()
    {
        var manager = CreatePackageManager();
        _host.Release();
        var plan = Plan(Package(Guid.NewGuid(), "Huge", "1.0.0", unpackedSize: long.MaxValue / 4));

        await Assert.ThrowsAsync<IOException>(() => manager.InstallPackages(plan, TestContext.Current.CancellationToken));

        Assert.Equal(0, _host.Requests);
    }

    [Fact]
    public async Task ArchivesAreDownloadedToTheCacheFolder()
    {
        var manager = CreatePackageManager();
        _host.Release();

        await manager.InstallPackages(Plan(Package(Guid.NewGuid(), "First", "1.0.0")), TestContext.Current.CancellationToken);

        Assert.True(Directory.Exists(Path.Join(_root, "cache", "plugin-downloads")));
        Assert.Empty(Directory.GetFiles(Path.Join(_root, "plugins"), "*.zip"));
    }

    [Fact]
    public async Task AnArchiveOfAnotherSizeThanTheManifestSaysIsRefused()
    {
        var manager = CreatePackageManager();
        _host.Release();

        Assert.Single(await manager.InstallPackages(Plan(Package(Guid.NewGuid(), "Right", "1.0.0", _host.Archive.Length)), TestContext.Current.CancellationToken));
        var refused = await Assert.ThrowsAsync<InvalidArchiveSizeException>(
            () => manager.InstallPackages(Plan(Package(Guid.NewGuid(), "Wrong", "1.0.0", _host.Archive.Length + 1)), TestContext.Current.CancellationToken)
        );

        Assert.Equal(_host.Archive.Length, refused.ActualSize);
        Assert.False(Directory.Exists(Path.Join(_root, "plugins", "Wrong-1.0.0-any")));
    }

    private static async Task<System.Collections.Generic.IReadOnlyList<PackageInstallQueueEntry>> WaitForRunning(PluginPackageManager manager)
    {
        for (var i = 0; i < 200; i++)
        {
            var queue = manager.GetInstallQueue();
            if (queue.Count > 0 && queue[0].StartedAt is not null)
                return queue;
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException("The first install never started.");
    }

    private PluginPackageManager CreatePackageManager()
    {
        var pluginManager = new Mock<IPluginManager>();
        pluginManager.Setup(manager => manager.GetPluginInfos(It.IsAny<Guid>())).Returns([]);
        pluginManager.Setup(manager => manager.LoadFromPath(It.IsAny<string>())).Returns<string>(path => Local(Path.GetFileName(path)));
        return new(
            AniDBTestDoubles.Configuration(new ServerSettings()),
            NullLogger<PluginPackageManager>.Instance,
            AniDBTestDoubles.HttpClientFactory(_host),
            pluginManager.Object,
            Mock.Of<IApplicationPaths>(paths => paths.PluginsPath == Path.Join(_root, "plugins") && paths.ApplicationPath == Path.Join(_root, "app") && paths.CachePath == Path.Join(_root, "cache")),
            Mock.Of<IQueueScheduler>
        );
    }

    private static PackageInstallPlan Plan(PackageInfo package)
        => new()
        {
            Package = package,
            Steps = [new() { Package = package, IsDependency = false, RequiredBy = [] }],
            Problems = [],
            OptionalDependencies = [],
        };

    private PackageInfo Package(Guid id, string name, string version, long packedSize = 0, long unpackedSize = 0)
    {
        var release = new PackageReleaseInfo
        {
            RepositoryID = null,
            Version = Version.Parse(version),
            Tag = null,
            SourceRevision = null,
            ReleasedAt = DateTime.UnixEpoch,
            Channel = ReleaseChannel.Stable,
            ReleaseNotes = null,
            Archives =
            [
                new()
                {
                    RuntimeIdentifier = IPluginManager.AnyRuntimeIdentifier,
                    AbstractionVersion = new(6, 0, 0),
                    ArchiveUrl = GatedArchiveHost.ArchiveUrl,
                    ArchiveChecksum = $"sha256:{Convert.ToHexString(SHA256.HashData(_host.Archive))}",
                    PackedSize = packedSize,
                    UnpackedSize = unpackedSize,
                },
            ],
        };
        return new()
        {
            Repository = null,
            Manifest = new()
            {
                PackageID = id,
                Name = name,
                Overview = string.Empty,
                Authors = string.Empty,
                RepositoryUrl = null,
                HomepageUrl = null,
                Tags = [],
                Thumbnail = null,
                Icon = null,
                Releases = [release],
                LastFetchedAt = DateTime.UnixEpoch,
            },
            Release = release,
            Archive = release.Archives[0],
            Plugin = null,
        };
    }

    private static LocalPluginInfo Local(string name)
        => new()
        {
            ID = Guid.NewGuid(),
            Name = name,
            Description = string.Empty,
            Version = new()
            {
                Version = new(1, 0, 0),
                RuntimeIdentifier = IPluginManager.AnyRuntimeIdentifier,
                AbstractionVersion = new(6, 0, 0),
                SourceRevision = null,
                ReleaseTag = null,
                Channel = ReleaseChannel.Stable,
                ReleasedAt = DateTime.UnixEpoch,
            },
            Authors = null,
            RepositoryUrl = null,
            HomepageUrl = null,
            Tags = [],
            Thumbnail = null,
            Icon = null,
            InstalledAt = DateTime.UnixEpoch,
            IsEnabled = true,
            IsActive = false,
            IsPinned = false,
            CanUninstall = true,
            Plugin = null,
            PluginType = null,
            ServiceRegistrationType = null,
            ApplicationRegistrationType = null,
            ContainingDirectory = null,
            DLLs = [$"{name}.dll"],
            Types = [],
            Dependencies = [],
        };

    /// <summary>
    ///   Serves one small archive, holding every download until released.
    /// </summary>
    private sealed class GatedArchiveHost : HttpMessageHandler
    {
        public const string ArchiveUrl = "https://repo.test/plugin.zip";

        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int _requests;

        public GatedArchiveHost()
        {
            using var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                using var writer = new StreamWriter(zip.CreateEntry("Plugin.dll").Open());
                writer.Write("not really a plugin");
            }

            Archive = stream.ToArray();
        }

        public byte[] Archive { get; }

        public int Requests => _requests;

        public void Release()
            => _gate.TrySetResult();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requests);
            await _gate.Task.WaitAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Archive) };
        }
    }
}

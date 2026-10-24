using System;
using System.Collections.Generic;
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
using Newtonsoft.Json;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Plugin;
using Shoko.Server.Settings;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Plugin;

/// <summary>
/// Tests for the plugin upgrades installed before the plugin scan, and for
/// the fallback to an older version when the new one fails at start.
/// </summary>
public class StartupPluginUpgradeTests : IDisposable
{
    private const string ArchiveUrl = "https://repo.test/plugin.zip";

    private const string RepositoryManifestUrl = "https://repo.test/manifest.json";

    private static readonly Guid _pluginId = new Shoko.Plugin.LegacyPlex.Plugin().ID;

    private static readonly Version _serverAbstraction = new(6, 0, 0);

    private readonly string _root = Directory.CreateDirectory(Path.Join(Path.GetTempPath(), $"shoko-startup-upgrade-{Guid.NewGuid():N}")).FullName;

    private readonly ArchiveHost _host = new();

    public void Dispose()
        => Directory.Delete(_root, recursive: true);

    #region Helpers

    private string PluginsPath => Path.Join(_root, "plugins");

    private static string LegacyPlexDll => Path.Combine(AppContext.BaseDirectory, "Shoko.Plugin.LegacyPlex.dll");

    private static bool IsCompatible(Version abstractionVersion, string runtimeIdentifier)
        => runtimeIdentifier is IPluginManager.AnyRuntimeIdentifier && abstractionVersion <= _serverAbstraction;

    private static InstalledPlugin Installed(string version, bool isPinned = false, bool isBundled = false)
        => new(_pluginId, "Plugin", Version.Parse(version), true, isPinned, isBundled);

    private static PackageReleaseInfo Release(string version, string abstractionVersion = "6.0.0", string checksum = "", Guid? repositoryID = null)
        => new()
        {
            RepositoryID = repositoryID,
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
                    AbstractionVersion = Version.Parse(abstractionVersion),
                    ArchiveUrl = ArchiveUrl,
                    ArchiveChecksum = checksum,
                },
            ],
        };

    private static PackageManifestInfo Manifest(params PackageReleaseInfo[] releases)
        => new()
        {
            PackageID = _pluginId,
            Name = "Plugin",
            Overview = string.Empty,
            Authors = string.Empty,
            RepositoryUrl = null,
            HomepageUrl = null,
            Tags = [],
            Thumbnail = null,
            Icon = null,
            Releases = releases,
            LastFetchedAt = DateTime.UnixEpoch,
        };

    private static IReadOnlyList<string> SelectVersions(
        IEnumerable<InstalledPlugin> installed,
        PackageManifestInfo manifest,
        bool skipBundled = false
    )
        => PluginPackageManager.SelectUpgrades(
                installed,
                [manifest],
                [],
                IsCompatible,
                "linux-x64",
                skipBundled
            )
            .Select(package => package.Release.Version.ToString())
            .ToList();

    private PluginPackageManager CreatePackageManager(bool isUpgradeOnStartEnabled = true)
    {
        var settings = new ServerSettings();
        settings.Plugins.Updates.IsUpgradeOnStartEnabled = isUpgradeOnStartEnabled;
        var pluginManager = new Mock<IPluginManager>();
        pluginManager.Setup(manager => manager.IsAbiAndRuntimeCompatible(It.IsAny<Version>(), It.IsAny<string>())).Returns<Version, string>(IsCompatible);
        pluginManager.Setup(manager => manager.RuntimeIdentifier).Returns("linux-x64");
        pluginManager.Setup(manager => manager.AbstractionVersion).Returns(_serverAbstraction);
        var reader = pluginManager.As<IInstalledPluginReader>();
        reader.Setup(manager => manager.ReadInstalledPlugins()).Returns(() => Directory.Exists(Path.Join(PluginsPath, "Plugin-1.0.0")) ? [Installed("1.0.0")] : []);
        reader.Setup(manager => manager.ReadPlugin(It.IsAny<string>())).Returns<string>(path => Directory.Exists(path) ? Installed("2.0.0") : null);
        return new(
            AniDBTestDoubles.Configuration(settings),
            NullLogger<PluginPackageManager>.Instance,
            AniDBTestDoubles.HttpClientFactory(_host),
            pluginManager.Object,
            Mock.Of<IApplicationPaths>(paths => paths.PluginsPath == PluginsPath && paths.ApplicationPath == Path.Join(_root, "app")),
            Mock.Of<IQueueScheduler>
        );
    }

    /// <summary>
    /// Installs the bundled test plugin as version 1.0.0, and a repository
    /// whose manifest offers <paramref name="releases"/>.
    /// </summary>
    private void SetUpInstalledPluginAndRepository(DateTime? lastFetchedAt, params PackageReleaseInfo[] releases)
    {
        var pluginDirectory = Directory.CreateDirectory(Path.Join(PluginsPath, "Plugin-1.0.0")).FullName;
        File.Copy(LegacyPlexDll, Path.Join(pluginDirectory, Path.GetFileName(LegacyPlexDll)));

        var repositoryID = Guid.NewGuid();
        var repositoryDirectory = Directory.CreateDirectory(Path.Join(PluginsPath, PluginPackageManager.Repositories, repositoryID.ToString())).FullName;
        var repository = new PackageRepositoryInfo
        {
            ID = repositoryID,
            Name = "Test",
            Url = RepositoryManifestUrl,
            StaleTime = TimeSpan.FromHours(1),
            LastFetchedAt = lastFetchedAt,
        };
        File.WriteAllText(Path.Join(repositoryDirectory, "info.json"), JsonConvert.SerializeObject(repository));
        var manifest = Manifest([.. releases.Select(release => Release(
            release.Version.ToString(),
            release.Archives[0].AbstractionVersion.ToString(),
            release.Archives[0].ArchiveChecksum,
            repositoryID
        ))]);
        File.WriteAllText(Path.Join(repositoryDirectory, "manifest.json"), JsonConvert.SerializeObject(new[] { manifest }));
    }

    /// <summary>
    /// Serves the bundled test plugin as a zip archive, and returns a
    /// release of the given version pointing at it.
    /// </summary>
    private PackageReleaseInfo ServeArchive(string version)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            zip.CreateEntryFromFile(LegacyPlexDll, Path.GetFileName(LegacyPlexDll));
        _host.Archive = stream.ToArray();
        return Release(version, checksum: $"sha256:{Convert.ToHexString(SHA256.HashData(_host.Archive))}");
    }

    private static PluginManager.InternalPluginInfo Candidate(
        string version,
        string? failureReason = null,
        bool isPinned = false,
        string? cannotLoadReason = null,
        bool isLegacy = false,
        bool isBundled = false,
        bool isKept = false
    )
        => new()
        {
            ID = _pluginId,
            Name = "Plugin",
            Description = string.Empty,
            Version = new()
            {
                Version = Version.Parse(version),
                RuntimeIdentifier = IPluginManager.AnyRuntimeIdentifier,
                AbstractionVersion = _serverAbstraction,
                SourceRevision = null,
                ReleaseTag = null,
                Channel = ReleaseChannel.Stable,
                ReleasedAt = DateTime.UnixEpoch,
            },
            Authors = null,
            RepositoryUrl = null,
            HomepageUrl = null,
            Tags = [],
            Priority = 0,
            InstalledAt = DateTime.UnixEpoch,
            IsPinned = isPinned,
            IsKept = isKept,
            IsEnabled = true,
            CanLoad = cannotLoadReason is null,
            CannotLoadReason = cannotLoadReason,
            IsLegacy = isLegacy,
            FailureReason = failureReason,
            CanUninstall = !isBundled,
            DllName = "Plugin",
            ContainingDirectory = null,
            DLLs = [$"/nonexistent/Plugin-{version}.dll"],
        };

    #endregion

    #region Selection

    [Fact]
    public void PinnedPluginIsNotUpgraded()
    {
        Assert.Empty(SelectVersions([Installed("1.0.0", isPinned: true)], Manifest(Release("2.0.0"))));
    }

    [Fact]
    public void OnlyACompatibleReleaseIsChosen()
    {
        var manifest = Manifest(Release("3.0.0", abstractionVersion: "6.1.0"), Release("2.0.0"), Release("1.5.0"));

        Assert.Equal(["2.0.0"], SelectVersions([Installed("1.0.0")], manifest));
    }

    [Fact]
    public void BundledPluginIsNotUpgradedWhenSkipped()
    {
        Assert.Empty(SelectVersions([Installed("1.0.0", isBundled: true)], Manifest(Release("2.0.0")), skipBundled: true));
        Assert.Equal(["2.0.0"], SelectVersions([Installed("1.0.0", isBundled: true)], Manifest(Release("2.0.0"))));
    }

    [Fact]
    public void NothingNewerThanTheInstalledVersionIsNoUpgrade()
    {
        Assert.Empty(SelectVersions([Installed("1.0.0"), Installed("2.0.0")], Manifest(Release("2.0.0"), Release("1.5.0"))));
    }

    #endregion

    #region Installing

    [Fact]
    public async Task InstallsTheNewestCompatibleRelease()
    {
        SetUpInstalledPluginAndRepository(DateTime.UtcNow, ServeArchive("2.0.0"));

        var installed = await CreatePackageManager().UpgradePluginsBeforeStart(_ => { }, TimeSpan.FromSeconds(30));

        Assert.Equal(["2.0.0"], installed.Select(package => package.Release.Version.ToString()));
        Assert.Single(Directory.GetDirectories(PluginsPath, "Plugin-2.0.0-*"));
    }

    [Fact]
    public async Task ARepositoryNotFetchedSinceShortlyBeforeTheStartIsFetchedAgain()
    {
        // Within the repository's own hour, but not within the five minutes on start.
        SetUpInstalledPluginAndRepository(DateTime.UtcNow.AddMinutes(-30), ServeArchive("2.0.0"));

        await CreatePackageManager().UpgradePluginsBeforeStart(_ => { }, TimeSpan.FromSeconds(30));

        Assert.Equal(1, _host.ManifestRequests);
    }

    [Fact]
    public async Task NothingHappensWhenTheOptionIsOff()
    {
        SetUpInstalledPluginAndRepository(null, ServeArchive("2.0.0"));

        var installed = await CreatePackageManager(isUpgradeOnStartEnabled: false).UpgradePluginsBeforeStart(_ => { }, TimeSpan.FromSeconds(30));

        Assert.Empty(installed);
        Assert.Equal(0, _host.Requests);
    }

    [Fact]
    public async Task UnreachableRepositorySkipsTheUpgrades()
    {
        SetUpInstalledPluginAndRepository(null, ServeArchive("2.0.0"));
        _host.IsOffline = true;

        var installed = await CreatePackageManager().UpgradePluginsBeforeStart(_ => { }, TimeSpan.FromSeconds(30));

        Assert.Empty(installed);
        Assert.Empty(Directory.GetDirectories(PluginsPath, "Plugin-2.0.0-*"));
    }

    [Fact]
    public async Task SpentBudgetContinuesWithThePluginsInstalled()
    {
        SetUpInstalledPluginAndRepository(DateTime.UtcNow, ServeArchive("2.0.0"));
        _host.Hangs = true;

        var installed = await CreatePackageManager().UpgradePluginsBeforeStart(_ => { }, TimeSpan.FromMilliseconds(200));

        Assert.Empty(installed);
        Assert.Empty(Directory.GetDirectories(PluginsPath, "Plugin-2.0.0-*"));
    }

    #endregion

    #region Version Choice

    [Fact]
    public void AVersionThatCannotLoadWithNothingOlderFailsTheStart()
    {
        var manager = TestPluginManager.Create(Mock.Of<IApplicationPaths>(paths => paths.PluginsPath == PluginsPath));
        var only = Candidate("2.0.0", cannotLoadReason: "Could not load a type it refers to.");

        var (toLoad, failureReason, passedOver) = manager.PickVersionToLoad([only], new Dictionary<string, string>());

        Assert.Same(only, toLoad);
        Assert.Equal("Could not load a type it refers to.", failureReason);
        Assert.Empty(passedOver);
    }

    [Fact]
    public void FailingVersionIsNotPassedOver()
    {
        var manager = TestPluginManager.Create(Mock.Of<IApplicationPaths>(paths => paths.PluginsPath == PluginsPath));
        var newer = Candidate("2.0.0", failureReason: "Built for another server.");

        var (toLoad, failureReason, passedOver) = manager.PickVersionToLoad([newer, Candidate("1.0.0")], new Dictionary<string, string>());

        Assert.Same(newer, toLoad);
        Assert.NotNull(failureReason);
        Assert.Empty(passedOver);
    }

    [Fact]
    public void VersionForANewerServerWaitsForTheServer()
    {
        var manager = TestPluginManager.Create(Mock.Of<IApplicationPaths>(paths => paths.PluginsPath == PluginsPath));
        var newer = Candidate("2.0.0", cannotLoadReason: "References a newer version of Shoko.Abstractions.");
        var older = Candidate("1.0.0");

        var (toLoad, _, passedOver) = manager.PickVersionToLoad([newer, older], new Dictionary<string, string>());

        Assert.Same(older, toLoad);
        Assert.Equal([newer], passedOver.Keys);
    }

    [Fact]
    public void AnInstalledCopyOfABundledVersionIsRemoved_UnlessPinnedOrKept()
    {
        var copy = Candidate("2.0.0");

        var copies = PluginManager.FindCopiesOfBundledVersions([
            Candidate("2.0.0", isBundled: true),
            copy,
            Candidate("2.0.0", isPinned: true),
            Candidate("2.0.0", isKept: true),
            Candidate("1.0.0"),
        ]);

        Assert.Same(copy, Assert.Single(copies));
    }

    [Fact]
    public void LegacyBuildIsReplacedByAnInstalledBuildForThisServer()
    {
        var legacy = Candidate("5.0.0", cannotLoadReason: "Built for the legacy abstractions.", isLegacy: true);
        var current = Candidate("6.4.0");

        var replacements = PluginManager.FindLegacyReplacements([legacy, current]);

        Assert.Same(current, Assert.Single(replacements, pair => pair.Key == legacy).Value);
    }

    [Fact]
    public void LegacyBuildWithoutALoadableReplacementIsKept()
    {
        var legacy = Candidate("5.0.0", cannotLoadReason: "Built for the legacy abstractions.", isLegacy: true);
        var newer = Candidate("7.0.0", cannotLoadReason: "References a newer version of Shoko.Abstractions.");

        var replacements = PluginManager.FindLegacyReplacements([legacy, newer]);

        Assert.Empty(replacements);
    }

    #endregion

    /// <summary>
    /// Serves the archive, and the manifest as missing, or nothing at all.
    /// </summary>
    private sealed class ArchiveHost : HttpMessageHandler
    {
        private int _requests;

        private int _manifestRequests;

        public byte[] Archive { get; set; } = [];

        public bool IsOffline { get; set; }

        public bool Hangs { get; set; }

        public int Requests => _requests;

        public int ManifestRequests => _manifestRequests;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requests);
            if (request.RequestUri!.ToString() == RepositoryManifestUrl)
                Interlocked.Increment(ref _manifestRequests);
            if (Hangs)
                await Task.Delay(Timeout.Infinite, cancellationToken);
            if (IsOffline)
                throw new HttpRequestException("No route to host.");

            return request.RequestUri!.ToString() == ArchiveUrl
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Archive) }
                : new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }
}

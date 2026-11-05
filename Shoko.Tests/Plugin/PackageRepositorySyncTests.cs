using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Plugin;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Plugin;

/// <summary>
/// Unit tests for <see cref="PluginPackageManager.SyncPackageRepository"/> on a
/// repository whose manifest lists its packages.
/// </summary>
public class PackageRepositorySyncTests : IDisposable
{
    private const string RepositoryUrl = "https://repo.test/manifest.json";

    private static readonly Guid _firstId = new("11111111-1111-1111-1111-111111111111");

    private static readonly Guid _secondId = new("22222222-2222-2222-2222-222222222222");

    private readonly string _root = Directory.CreateDirectory(Path.Join(Path.GetTempPath(), $"shoko-package-repository-sync-{Guid.NewGuid():N}")).FullName;

    private readonly ManifestHost _host = new();

    private readonly PluginPackageManager _manager;

    private readonly PackageRepositoryInfo _repository = new()
    {
        ID = new("99999999-9999-9999-9999-999999999999"),
        Name = "Test",
        Url = RepositoryUrl,
        StaleTime = TimeSpan.FromHours(1),
        LastFetchedAt = null,
    };

    public PackageRepositorySyncTests()
    {
        _manager = new(
            null!,
            NullLogger<PluginPackageManager>.Instance,
            AniDBTestDoubles.HttpClientFactory(_host),
            Mock.Of<IPluginManager>(),
            Mock.Of<IApplicationPaths>(paths => paths.PluginsPath == _root),
            Mock.Of<IQueueScheduler>
        );
        Serve(PackageManifest(_firstId, "First"), PackageManifest(_secondId, "Second"));
    }

    /// <summary>
    /// Serves the repository's manifest as a list of the given packages.
    /// </summary>
    private void Serve(params string[] packages)
        => _host.Json[RepositoryUrl] = $"[{string.Join(",", packages)}]";

    public void Dispose()
        => Directory.Delete(_root, recursive: true);

    private static string PackageManifest(Guid id, string name)
        => $$"""
            { "id": "{{id}}", "name": "{{name}}", "overview": "Overview.", "authors": "Author", "tags": [], "releases": [] }
            """;

    private async Task<IReadOnlyList<string>> SyncAndListPackageNames()
    {
        await _manager.SyncPackageRepository(_repository, forceSync: true, TestContext.Current.CancellationToken);
        var manifests = await _manager.GetAvailablePackageManifests(allowSync: false, cancellationToken: TestContext.Current.CancellationToken);
        return manifests.Select(manifest => manifest.Name).ToList();
    }

    [Fact]
    public async Task UnavailableRepositoryManifestKeepsTheLastSync()
    {
        Assert.Equal(["First", "Second"], await SyncAndListPackageNames());
        var lastFetchedAt = _repository.LastFetchedAt;

        _host.Json.Remove(RepositoryUrl);

        Assert.Equal(["First", "Second"], await SyncAndListPackageNames());
        Assert.Equal(lastFetchedAt, _repository.LastFetchedAt);
    }

    [Fact]
    public async Task AnEntryWithoutANameIsLeftOut()
    {
        Serve(PackageManifest(_firstId, "First"), $$"""{ "id": "{{_secondId}}" }""");

        Assert.Equal(["First"], await SyncAndListPackageNames());
    }

    [Fact]
    public async Task APackageListedTwiceIsReadOnce()
    {
        Serve(PackageManifest(_firstId, "First"), PackageManifest(_firstId, "First Again"));

        Assert.Equal(["First"], await SyncAndListPackageNames());
    }

    [Fact]
    public async Task ARepositoryOfOnePackageIsRead()
    {
        _host.Json[RepositoryUrl] = PackageManifest(_firstId, "First");

        Assert.Equal(["First"], await SyncAndListPackageNames());
    }

    [Fact]
    public async Task HttpArchiveInAnHttpsManifestIsRefused()
    {
        Serve($$"""
            {
              "id": "{{_firstId}}", "name": "First", "overview": "Overview.", "authors": "Author", "tags": [],
              "releases": [
                {
                  "version": "1.0.0", "tag": null, "source_revision": null, "channel": "stable", "released_at": "2026-11-01T00:00:00Z",
                  "archives": [
                    { "runtime": "any", "abstraction": "6.0.0", "url": "https://first.test/first.zip", "checksum": "sha256:00" },
                    { "runtime": "any", "abstraction": "6.0.0", "url": "http://first.test/first.zip", "checksum": "sha256:00" }
                  ]
                }
              ]
            }
            """);

        await SyncAndListPackageNames();
        var manifests = await _manager.GetAvailablePackageManifests(allowSync: false, cancellationToken: TestContext.Current.CancellationToken);
        var release = Assert.Single(manifests.Single(manifest => manifest.PackageID == _firstId).Releases);

        Assert.Equal(["https://first.test/first.zip"], release.Archives.Select(archive => archive.ArchiveUrl));
        Assert.Contains("http://first.test/first.zip", Assert.Single(release.Refusals));
    }

    [Fact]
    public async Task ExtensionsArePassedThroughAtEveryLevel()
    {
        Serve($$"""
            {
              "id": "{{_firstId}}", "name": "First", "overview": "Overview.", "authors": "Author", "tags": [],
              "extensions": { "badge": "gold" },
              "releases": [
                {
                  "version": "1.0.0", "tag": null, "source_revision": null, "channel": "stable", "released_at": "2026-11-01T00:00:00Z",
                  "extensions": { "downloads": 12 },
                  "archives": [
                    { "runtime": "any", "abstraction": "6.0.0", "url": "https://first.test/first.zip", "checksum": "sha256:00", "extensions": { "downloads": 12, "nested": { "a": [1] } } }
                  ]
                }
              ]
            }
            """);

        await SyncAndListPackageNames();
        var manifests = await _manager.GetAvailablePackageManifests(allowSync: false, cancellationToken: TestContext.Current.CancellationToken);
        var manifest = manifests.Single(manifest => manifest.PackageID == _firstId);
        var release = Assert.Single(manifest.Releases);
        var archive = Assert.Single(release.Archives);

        Assert.Equal("gold", (string?)manifest.Extensions?["badge"]);
        Assert.Equal(12, (int?)release.Extensions?["downloads"]);
        Assert.Equal(1, (int?)archive.Extensions?["nested"]?["a"]?[0]);
    }

    [Theory]
    [InlineData("https://repo.test/manifest.json", "http://repo.test/a.zip", true)]
    [InlineData("https://repo.test/manifest.json", "https://repo.test/a.zip", false)]
    [InlineData("https://repo.test/manifest.json", "a.zip", false)]
    [InlineData("http://repo.test/manifest.json", "http://repo.test/a.zip", false)]
    [InlineData("http://repo.test/manifest.json", "https://repo.test/a.zip", false)]
    public void ADownloadMayNotBeLessSecureThanItsManifest(string manifestUrl, string url, bool refused)
        => Assert.Equal(refused, PluginPackageManager.IsDowngrade(manifestUrl, url));

    /// <summary>
    /// Serves the manifests in <see cref="Json"/> by address, and answers 404
    /// for every other address.
    /// </summary>
    private sealed class ManifestHost : HttpMessageHandler
    {
        public Dictionary<string, string> Json { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(Json.TryGetValue(request.RequestUri!.ToString(), out var json)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") }
                : new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("404: Not Found", Encoding.UTF8, "text/plain") });
    }
}

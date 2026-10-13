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
/// repository whose manifest links to one manifest per package, some of them
/// unavailable.
/// </summary>
public class PackageRepositorySyncTests : IDisposable
{
    private const string RepositoryUrl = "https://repo.test/manifest.json";

    private const string FirstUrl = "https://first.test/manifest.json";

    private const string SecondUrl = "https://second.test/manifest.json";

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
            Mock.Of<IQueueScheduler>()
        );
        _host.Json[RepositoryUrl] = $$"""
            [
              { "id": "{{_firstId}}", "type": "manifest", "url": "{{FirstUrl}}" },
              { "id": "{{_secondId}}", "type": "manifest", "url": "{{SecondUrl}}" }
            ]
            """;
        _host.Json[FirstUrl] = PackageManifest(_firstId, "First");
        _host.Json[SecondUrl] = PackageManifest(_secondId, "Second");
    }

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
    public async Task UnavailableLinkedManifestWithoutCacheIsSkipped()
    {
        _host.Json.Remove(SecondUrl);

        Assert.Equal(["First"], await SyncAndListPackageNames());
        Assert.NotNull(_repository.LastFetchedAt);
    }

    [Fact]
    public async Task UnavailableLinkedManifestKeepsItsCachedCopy()
    {
        Assert.Equal(["First", "Second"], await SyncAndListPackageNames());

        _host.Json[FirstUrl] = PackageManifest(_firstId, "First Renamed");
        _host.Json.Remove(SecondUrl);

        Assert.Equal(["First Renamed", "Second"], await SyncAndListPackageNames());
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

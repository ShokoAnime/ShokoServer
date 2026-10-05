using System;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Plugin.Tmdb;
using Shoko.Plugin.Tmdb.Api;
using Shoko.Plugin.Tmdb.Metadata;
using Shoko.Plugin.Tmdb.Services;

namespace Shoko.Tests.Plugin.Tmdb;

/// <summary>
///   The TMDb plugin's services wired together over the fixtures and the
///   fake stores, the way the plugin's registration wires them.
/// </summary>
internal sealed class TmdbServiceHarness : IDisposable
{
    private readonly Lazy<TmdbApiClient> _apiClient;

    private readonly Lazy<TmdbStores> _stores;

    /// <summary>
    ///   Creates the harness, with every optional download switched on.
    /// </summary>
    /// <param name="timeProvider">The clock; the system's when left out.</param>
    public TmdbServiceHarness(TimeProvider? timeProvider = null)
    {
        _apiClient = new(() => TmdbTestClient.Create(Routes, Configuration, timeProvider));
        _stores = new(StoreData.Build);
        CrossReferences.Setup(mock => mock.GetLinksTo(It.IsAny<MetadataGuid>())).Returns([]);
        CrossReferences.Setup(mock => mock.GetEpisodeLinksInto(It.IsAny<MetadataGuid>())).Returns([]);
        MetadataService.Setup(mock => mock.GetSeriesCrossReferences(It.IsAny<int>(), It.IsAny<MetadataSource?>())).Returns([]);
        Linking.Setup(mock => mock.GetCrossSourceHints(It.IsAny<MetadataSource>(), It.IsAny<int>())).Returns([]);
    }

    #region Parts

    /// <summary>The answers TMDb gives.</summary>
    public TmdbRoutes Routes { get; } = new();

    /// <summary>The plugin's configuration.</summary>
    public TmdbConfiguration Configuration { get; } = new()
    {
        UserApiKey = "test-key",
        AutoDownloadAlternateOrdering = true,
    };

    /// <summary>What the stores hold.</summary>
    public TmdbFakeStores StoreData { get; } = new();

    /// <summary>The core's links.</summary>
    public Mock<IMetadataCrossReferenceStore> CrossReferences { get; } = new();

    /// <summary>The core's linking service.</summary>
    public Mock<IMetadataLinkingService> Linking { get; } = new();

    /// <summary>The core's matching engine.</summary>
    public Mock<IMetadataMatchingEngine> Engine { get; } = new();

    /// <summary>The core's metadata service.</summary>
    public Mock<IMetadataService> MetadataService { get; } = new();

    /// <summary>The client.</summary>
    public TmdbApiClient ApiClient => _apiClient.Value;

    /// <summary>The stores, as the plugin takes them.</summary>
    public TmdbStores Stores => _stores.Value;

    #endregion

    #region Services

    /// <summary>The tag service.</summary>
    public TmdbTagService Tags => field ??= new(ApiClient, Stores.Tags, NullLogger<TmdbTagService>.Instance);

    /// <summary>The linking service.</summary>
    public TmdbLinkingService Linker => field ??= new(
        Stores.Series,
        CrossReferences.Object,
        Engine.Object,
        MetadataService.Object,
        TmdbTestClient.Configuration(Configuration).Provider
    );

    /// <summary>The refresh service.</summary>
    public TmdbRefreshService Refresh => field ??= new(
        ApiClient,
        Stores,
        TmdbTestClient.Configuration(Configuration).Provider,
        NullLogger<TmdbRefreshService>.Instance
    );

    /// <summary>The entity refresh service.</summary>
    public TmdbEntityRefreshService Entities => field ??= new(ApiClient, Stores, TmdbTestClient.Configuration(Configuration).Provider, NullLogger<TmdbEntityRefreshService>.Instance);

    /// <summary>The image service.</summary>
    public TmdbImageService Images => field ??= new(ApiClient, Stores, Entities);

    /// <summary>The search service.</summary>
    public TmdbSearchService Search => field ??= new(
        ApiClient,
        Stores,
        Tags,
        Engine.Object,
        MetadataService.Object,
        Linking.Object,
        TmdbTestClient.Configuration(Configuration).Provider,
        NullLogger<TmdbSearchService>.Instance
    );

    /// <summary>The provider.</summary>
    public TmdbMetadataProvider Provider => field ??= new(ApiClient, Refresh, Entities, Search, Linker, Images, MetadataService.Object, NullLogger<TmdbMetadataProvider>.Instance);

    #endregion

    #region Fixtures

    /// <summary>
    ///   Routes the whole of show 1001: the show, its seasons, episodes and
    ///   episode group.
    /// </summary>
    /// <returns>The harness.</returns>
    public TmdbServiceHarness RouteShow()
    {
        Routes
            .Fixture("tv/1001", "show-1001.json")
            .Fixture("tv/1001/season/0", "season-1001-0.json")
            .Fixture("tv/1001/season/1", "season-1001-1.json")
            .Fixture("tv/1001/season/0/episode/1", "episode-1001-0-1.json")
            .Fixture("tv/1001/season/1/episode/1", "episode-1001-1-1.json")
            .Fixture("tv/1001/season/1/episode/2", "episode-1001-1-2.json")
            .Fixture("tv/episode_group/5acf93e60e0a26346d0000ce", "episode-group-5acf93e60e0a26346d0000ce.json");
        return this;
    }

    /// <summary>
    ///   Routes movie 7001 and its collection.
    /// </summary>
    /// <returns>The harness.</returns>
    public TmdbServiceHarness RouteMovie()
    {
        Routes
            .Fixture("movie/7001", "movie-7001.json")
            .Fixture("collection/8001", "collection-8001.json");
        return this;
    }

    /// <summary>
    ///   Routes TMDb's genre lists and image server.
    /// </summary>
    /// <returns>The harness.</returns>
    public TmdbServiceHarness RouteBasics()
    {
        Routes
            .Fixture("genre/tv/list", "genres-tv.json")
            .Fixture("genre/movie/list", "genres-movie.json")
            .Fixture("configuration", "configuration.json");
        return this;
    }

    #endregion

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_apiClient.IsValueCreated)
            _apiClient.Value.Dispose();
    }
}

using System;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Plugin;
using Shoko.Plugin.Tmdb.Api;
using Shoko.Plugin.Tmdb.Metadata;
using Shoko.Plugin.Tmdb.Services;

namespace Shoko.Plugin.Tmdb;

/// <summary>
///   The bundled first-party plugin for TMDB, which serves the <c>tmdb</c>
///   source.
/// </summary>
public class Plugin : IPlugin, IPluginServiceRegistration
{
    /// <summary>
    ///   The plugin's ID. Kept as a literal, because the build reads the
    ///   embedded identity from this file.
    /// </summary>
    public Guid ID { get; private init; } = new("85d0c34f-240f-4da3-8512-d0b45118a9f4");

    /// <summary>
    ///   The embedded resource of the plugin's thumbnail.
    /// </summary>
    internal const string ThumbnailResourceName = "Shoko.Plugin.Tmdb.Assets.thumbnail.svg";

    /// <summary>
    ///   The embedded resource of the plugin's icon, which is TMDB's source icon
    ///   too.
    /// </summary>
    internal const string IconResourceName = "Shoko.Plugin.Tmdb.Assets.icon.svg";

    /// <inheritdoc/>
    public string Name { get; private init; } = "TMDB";

    /// <inheritdoc/>
    public string Description { get; private init; } = """
        Supplies TMDB metadata for shows, movies, collections, people, studios
        and networks. Bundled with the server.
        """;

    /// <inheritdoc/>
    public string? EmbeddedThumbnailResourceName => ThumbnailResourceName;

    /// <inheritdoc/>
    public string? EmbeddedIconResourceName => IconResourceName;

    /// <summary>
    ///   Registers the plugin's services and provider, as singletons the
    ///   server and the plugin share.
    /// </summary>
    /// <param name="serviceCollection">The services.</param>
    /// <param name="applicationPaths">Unused.</param>
    public static void RegisterServices(IServiceCollection serviceCollection, IApplicationPaths applicationPaths)
    {
        serviceCollection.AddSingleton<TmdbApiClient>();
        serviceCollection.AddSingleton<TmdbStores>();
        serviceCollection.AddSingleton<TmdbTagService>();
        serviceCollection.AddSingleton<TmdbLinkingService>();
        serviceCollection.AddSingleton<TmdbRefreshService>();
        serviceCollection.AddSingleton<TmdbEntityRefreshService>();
        serviceCollection.AddSingleton<TmdbImageService>();
        serviceCollection.AddSingleton<TmdbSearchService>();
        serviceCollection.AddSingleton<TmdbMetadataProvider>();
        serviceCollection.AddHostedService<TmdbBackgroundService>();
    }
}

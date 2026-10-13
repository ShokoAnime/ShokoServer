using System;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Plugin;

namespace Shoko.Plugin.LegacyPlex;

/// <summary>
///   The bundled first-party plugin for the legacy Plex support: the Plex
///   webhook, the Plex account linking and the watched state sync.
/// </summary>
public class Plugin : IPlugin, IPluginServiceRegistration
{
    /// <summary>
    ///   The plugin's ID. Kept as a literal, because the build reads the
    ///   embedded identity from this file.
    /// </summary>
    public Guid ID { get; private init; } = new("d5b194ef-6f9d-455b-beb3-b23af899bb21");

    /// <summary>
    ///   The embedded resource of the plugin's thumbnail.
    /// </summary>
    internal const string ThumbnailResourceName = "Shoko.Plugin.LegacyPlex.Assets.thumbnail.svg";

    /// <summary>
    ///   The embedded resource of the plugin's icon.
    /// </summary>
    internal const string IconResourceName = "Shoko.Plugin.LegacyPlex.Assets.icon.svg";

    /// <inheritdoc/>
    public string Name { get; private init; } = "Legacy Plex Sync Support";

    /// <inheritdoc/>
    public string Description { get; private init; } = """
        The legacy Plex webhook and watched state sync. Bundled with the server.
        """;

    /// <inheritdoc/>
    public string? EmbeddedThumbnailResourceName => ThumbnailResourceName;

    /// <inheritdoc/>
    public string? EmbeddedIconResourceName => IconResourceName;

    /// <summary>
    ///   Registers the Plex clients and the HTTP client they talk to Plex
    ///   with.
    /// </summary>
    /// <param name="serviceCollection">The services.</param>
    /// <param name="applicationPaths">Unused.</param>
    public static void RegisterServices(IServiceCollection serviceCollection, IApplicationPaths applicationPaths)
    {
        serviceCollection.AddHttpClient(PlexHelper.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(10));
        serviceCollection.AddSingleton<PlexHelperProvider>();
    }
}

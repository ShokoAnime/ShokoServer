using System;
using Shoko.Abstractions.Plugin;

namespace Shoko.Plugin.WebAOM;

/// <summary>
///   The bundled first-party plugin for the WebAOM renamer, the server's
///   default relocation provider.
/// </summary>
public class Plugin : IPlugin
{
    /// <summary>
    ///   The plugin's ID. Kept as a literal, because the build reads the
    ///   embedded identity from this file.
    /// </summary>
    public Guid ID { get; private init; } = new("8c8d75f2-cd0c-5c0b-9a9e-8c2c6d3e5f1a");

    /// <summary>
    ///   The embedded resource of the plugin's thumbnail.
    /// </summary>
    internal const string ThumbnailResourceName = "Shoko.Plugin.WebAOM.Assets.thumbnail.svg";

    /// <summary>
    ///   The embedded resource of the plugin's icon.
    /// </summary>
    internal const string IconResourceName = "Shoko.Plugin.WebAOM.Assets.icon.svg";

    /// <inheritdoc/>
    public string Name { get; private init; } = "WebAOM";

    /// <inheritdoc/>
    public string Description { get; private init; } = """
        The legacy renamer, based on WebAOM's renamer. Bundled with the server.
        """;

    /// <inheritdoc/>
    public string? EmbeddedThumbnailResourceName => ThumbnailResourceName;

    /// <inheritdoc/>
    public string? EmbeddedIconResourceName => IconResourceName;
}

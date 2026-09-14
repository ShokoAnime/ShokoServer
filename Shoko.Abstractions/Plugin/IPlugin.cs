using System;
using System.Collections.Generic;
using Shoko.Abstractions.Plugin.Models;

namespace Shoko.Abstractions.Plugin;

/// <summary>
///   Interface for plugins to register themselves automagically.
/// </summary>
public interface IPlugin
{
    /// <summary>
    ///   Unique ID of the plugin.
    /// </summary>
    Guid ID { get; }

    /// <summary>
    ///   Friendly name of the plugin.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///   Description of the plugin.
    /// </summary>
    string? Description { get => null; }

    /// <summary>
    ///   Get the embedded thumbnail image resource name for the plugin. Used to
    ///   load the thumbnail image from the embedded resources. Must be an
    ///   absolute resource name, including the assembly name.
    /// </summary>
    /// <example>
    ///   A valid resource name for the thumbnail image located at
    ///   "assets/Thumbnail.png" for the example plugin "Shoko.Plugin.Example"
    ///   would be:
    ///   <code>
    ///   "Shoko.Plugin.Example.assets.Thumbnail.png"
    ///   </code>
    /// </example>
    string? EmbeddedThumbnailResourceName { get => null; }

    /// <summary>
    ///   Get the pages exposed by the plugin.
    /// </summary>
    /// <returns>
    ///   The pages exposed by the plugin.
    /// </returns>
    public IReadOnlyList<PluginPage> GetPages() => [];

    /// <summary>
    ///   Get the features advertised by the plugin to clients. Called whenever
    ///   a client asks for the features, so a plugin can leave out a feature
    ///   that isn't usable with its current configuration.
    /// </summary>
    /// <remarks>
    ///   Features with an invalid name, version, or visibility are dropped, as
    ///   are later features with the same name. See
    ///   <see cref="PluginFeature.IsValid"/>.
    /// </remarks>
    /// <returns>
    ///   The features advertised by the plugin.
    /// </returns>
    public IReadOnlyList<PluginFeature> GetFeatures() => [];
}

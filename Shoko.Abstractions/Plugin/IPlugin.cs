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
    ///   Get the embedded icon image resource name for the plugin. Used to
    ///   load the icon image from the embedded resources. Must be an absolute
    ///   resource name, including the assembly name.
    /// </summary>
    /// <remarks>
    ///   The icon is the plugin's mark, shown beside its name wherever the
    ///   list has no room for the thumbnail. Supply something square that is
    ///   still readable at 16 pixels; the server neither crops nor scales it.
    /// </remarks>
    /// <example>
    ///   A valid resource name for the icon image located at
    ///   "assets/Icon.png" for the example plugin "Shoko.Plugin.Example"
    ///   would be:
    ///   <code>
    ///   "Shoko.Plugin.Example.assets.Icon.png"
    ///   </code>
    /// </example>
    string? EmbeddedIconResourceName { get => null; }

    /// <summary>
    ///   Called once during start-up, after the plugins are initialized and
    ///   before the database is opened, to acquire the services the plugin
    ///   needs. The default implementation is a no-op; override only when
    ///   needed. Throwing stops the server finishing its start-up, so take
    ///   services here and leave the work that uses them for later.
    /// </summary>
    /// <remarks>
    ///   A plugin is built twice. Discovery builds it with
    ///   <see cref="System.Activator.CreateInstance(System.Type)"/> to read the
    ///   identity above, which needs a public parameterless constructor and
    ///   throws without one, so a plugin that takes a constructor dependency
    ///   never loads at all. This hook is how a plugin reaches the container
    ///   instead. It mirrors <c>IQueueJob.Setup</c>, which exists for the same
    ///   reason.
    /// </remarks>
    /// <param name="serviceProvider">
    ///   The service provider to resolve services from.
    /// </param>
    void Setup(IServiceProvider serviceProvider) { }

    /// <summary>
    ///   Called once after every plugin has been set up, and before the web
    ///   host starts. The default implementation is a no-op; override only
    ///   when needed. Throwing stops the server finishing its start-up.
    /// </summary>
    /// <remarks>
    ///   <see cref="Setup"/> runs in load order, and a dependency always loads
    ///   before the plugins depending on it, so a plugin that owns something
    ///   other plugins contribute to has been set up before any of its
    ///   contributors. Contributions belong in <see cref="Setup"/>; whatever
    ///   has to see all of them, such as freezing a registry, belongs here.
    /// </remarks>
    void Ready() { }

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

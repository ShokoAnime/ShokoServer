using System;
using Shoko.Plugin.Abstractions.Config;
using Shoko.Plugin.Abstractions.Plugin;

namespace Shoko.Plugin.Abstractions.Relocation;

/// <summary>
///   Contains information about a <see cref="IRelocationProvider"/>.
/// </summary>
public class RelocationProviderInfo
{
    /// <summary>
    ///   The unique ID of the provider.
    /// </summary>
    public required Guid ID { get; init; }

    /// <summary>
    ///   The version of the release info provider.
    /// </summary>
    public required Version Version { get; init; }

    /// <summary>
    ///   The display name of the release info provider.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///   Describes what the release info provider is for.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    ///   This should be true if the renamer supports operating on unrecognized
    ///   files.
    /// </summary>
    public bool SupportsUnrecognized { get => Provider.SupportsUnrecognized; }

    /// <summary>
    ///   Indicates that the renamer supports moving files. That is, changing
    ///   the directory the file is in.
    /// </summary>
    public bool SupportsMoving { get => Provider.SupportsMoving; }

    /// <summary>
    ///   Indicates that the renamer supports renaming files. That is, changing
    ///   the name of the file itself.
    /// </summary>
    public bool SupportsRenaming { get => Provider.SupportsRenaming; }

    /// <summary>
    ///   The <see cref="IRelocationProvider"/> that this info is for.
    /// </summary>
    public required IRelocationProvider Provider { get; init; }

    /// <summary>
    ///   Information about the configuration that the release info provider
    ///   uses.
    /// </summary>
    public required ConfigurationInfo? ConfigurationInfo { get; init; }

    /// <summary>
    ///   Information about the plugin that the release info provider belongs
    ///   to.
    /// </summary>
    public required PluginInfo PluginInfo { get; init; }
}

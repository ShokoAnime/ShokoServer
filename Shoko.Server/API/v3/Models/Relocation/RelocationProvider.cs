using System;
using Shoko.Plugin.Abstractions.Relocation;
using Shoko.Server.API.v3.Models.Configuration;
using Shoko.Server.API.v3.Models.Plugin;

#nullable enable
namespace Shoko.Server.API.v3.Models.Relocation;

/// <summary>
///   A release info provider.
/// </summary>
/// <param name="info">
///   Internal release info provider.
/// </param>
public class RelocationProvider(RelocationProviderInfo info)
{
    /// <summary>
    ///   The unique ID of the provider.
    /// </summary>
    public Guid ID { get; init; } = info.ID;

    /// <summary>
    ///   The version of the release info provider.
    /// </summary>
    public Version Version { get; init; } = info.Version;

    /// <summary>
    ///   The display name of the release info provider.
    /// </summary>
    public string Name { get; init; } = info.Name;

    /// <summary>
    ///   Describes what the release info provider is for.
    /// </summary>
    public string Description { get; init; } = string.IsNullOrEmpty(info.Description) ? string.Empty : info.Description;

    /// <summary>
    ///   This should be true if the renamer supports operating on unrecognized
    ///   files.
    /// </summary>
    public bool SupportsUnrecognized { get; init; } = info.SupportsUnrecognized;

    /// <summary>
    ///   Indicates that the renamer supports moving files. That is, changing
    ///   the directory the file is in.
    /// </summary>
    public bool SupportsMoving { get; init; } = info.SupportsMoving;

    /// <summary>
    ///   Indicates that the renamer supports renaming files. That is, changing
    ///   the name of the file itself.
    /// </summary>
    public bool SupportsRenaming { get; init; } = info.SupportsRenaming;

    /// <summary>
    ///   Information about the configuration the release info provider uses.
    /// </summary>
    public ConfigurationInfo? Configuration { get; init; } = info.ConfigurationInfo is null ? null : new(info.ConfigurationInfo);

    /// <summary>
    ///   Information about the plugin that the release info provider belongs to.
    /// </summary>
    public PluginInfo Plugin { get; init; } = new(info.PluginInfo);
}

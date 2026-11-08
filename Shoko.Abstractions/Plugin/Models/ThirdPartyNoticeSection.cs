using System;
using System.Collections.Generic;

namespace Shoko.Abstractions.Plugin.Models;

/// <summary>
///   The third-party notices of one loaded plugin, the core included.
/// </summary>
public sealed class ThirdPartyNoticeSection
{
    /// <summary>
    ///   The plugin's ID.
    /// </summary>
    public required Guid PluginID { get; init; }

    /// <summary>
    ///   The plugin's name.
    /// </summary>
    public required string PluginName { get; init; }

    /// <summary>
    ///   The plugin's version.
    /// </summary>
    public required Version PluginVersion { get; init; }

    /// <summary>
    ///   Whether this is the core's section.
    /// </summary>
    public required bool IsCore { get; init; }

    /// <summary>
    ///   The plugin's own licence, heading its section, or <c>null</c> when its
    ///   notices do not give one.
    /// </summary>
    public ThirdPartyNotice? Project { get; init; }

    /// <summary>
    ///   The third-party packages and assets it ships, sorted by ID.
    /// </summary>
    public required IReadOnlyList<ThirdPartyNotice> Packages { get; init; }
}

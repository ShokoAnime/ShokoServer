using System;
using Shoko.Abstractions.Plugin.Models;

namespace Shoko.Abstractions.Config;

/// <summary>
///   A user configuration as one user sees it: registered, stored, or both.
/// </summary>
public sealed class UserConfigurationInfo
{
    /// <summary>
    ///   The configuration's kebab-case ID.
    /// </summary>
    public required string ID { get; init; }

    /// <summary>
    ///   The ID of the plugin that registered the configuration, or that
    ///   registered it when it was last saved.
    /// </summary>
    public required Guid PluginID { get; init; }

    /// <summary>
    ///   The configuration's display name, or its ID when it was registered
    ///   without one or is not registered.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///   The configuration's description, or <c>null</c> when it has none or
    ///   is not registered.
    /// </summary>
    public required string? Description { get; init; }

    /// <summary>
    ///   Whether a loaded plugin registered the configuration, so it can be
    ///   saved.
    /// </summary>
    public required bool IsRegistered { get; init; }

    /// <summary>
    ///   Whether the registered configuration has a schema it is validated
    ///   against.
    /// </summary>
    public required bool HasSchema { get; init; }

    /// <summary>
    ///   The configuration type describing it, when it was registered with
    ///   one, for its schema and generated UI.
    /// </summary>
    public required ConfigurationInfo? ConfigurationInfo { get; init; }

    /// <summary>
    ///   The plugin that registered the configuration, or <c>null</c> when it
    ///   is not loaded.
    /// </summary>
    public required LocalPluginInfo? PluginInfo { get; init; }

    /// <summary>
    ///   When the configuration was last saved, in UTC, or <c>null</c> when
    ///   none is stored.
    /// </summary>
    public required DateTime? UpdatedAt { get; init; }
}

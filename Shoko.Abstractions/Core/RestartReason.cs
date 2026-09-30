using System;
using Shoko.Abstractions.Core.Services;

namespace Shoko.Abstractions.Core;

/// <summary>
///   One reason the server needs a restart for a change to take effect, as
///   listed by <see cref="ISystemService.RestartReasons"/>. Reasons live in
///   memory, so a restart clears them all.
/// </summary>
public sealed class RestartReason
{
    /// <summary>
    ///   Where the reason came from.
    /// </summary>
    public required RestartReasonSource Source { get; init; }

    /// <summary>
    ///   The plugin the reason belongs to: the core plugin for the
    ///   configuration and plugin state reasons, and the plugin that raised it
    ///   for a <see cref="RestartReasonSource.Plugin"/> reason.
    /// </summary>
    public required Guid PluginID { get; init; }

    /// <summary>
    ///   A key that stays the same for as long as the reason stands, unique
    ///   within its <see cref="Source"/> and <see cref="PluginID"/>:
    ///   <c>configuration</c> for the configuration reason, <c>plugins</c> for
    ///   the plugin state reason, and a generated ID for a reason a plugin
    ///   raised.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    ///   A short, human-readable description of what changed.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    ///   When the reason was first raised, in UTC. It keeps that time for as
    ///   long as it stands.
    /// </summary>
    public required DateTime RaisedAt { get; init; }
}

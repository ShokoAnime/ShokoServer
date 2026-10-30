using System;
using System.Collections.Generic;
using Shoko.Abstractions.Plugin.Enums;

namespace Shoko.Abstractions.Plugin.Models;

/// <summary>
///   One plugin version in a <see cref="PluginChangePlan"/>.
/// </summary>
public sealed class PluginChangePlanEntry
{
    /// <summary>
    ///   The plugin version.
    /// </summary>
    public required LocalPluginInfo Plugin { get; init; }

    /// <summary>
    ///   What happens to it.
    /// </summary>
    public required PluginChange Change { get; init; }

    /// <summary>
    ///   Why.
    /// </summary>
    public required PluginChangeReason Reason { get; init; }

    /// <summary>
    ///   For a plugin that stays because others need it, the plugins that
    ///   still depend on it once the plan is carried out. For one that stops
    ///   loading, the required dependencies it no longer has.
    /// </summary>
    public IReadOnlyList<Guid> Related { get; init; } = [];
}

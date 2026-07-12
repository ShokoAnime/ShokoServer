using System;
using System.Collections.Generic;
using Shoko.Abstractions.Action.Enums;
using Shoko.Server.API.v3.Models.Plugin;

namespace Shoko.Server.API.v3.Models.Action;

/// <summary>
///   Describes a registered executable action for API consumers.
/// </summary>
public sealed class ActionInfo
{
    /// <summary>
    ///   The stable identifier for this action.
    /// </summary>
    public required Guid ID { get; init; }

    /// <summary>
    ///   The human-readable name of the action.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///   A description of what the action does.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    ///   Whether the action requires explicit user confirmation before execution.
    /// </summary>
    public required bool RequiresConfirmation { get; init; }

    /// <summary>
    ///   The scopes at which this action can operate, filtered to the ones the
    ///   current user is permitted to execute.
    /// </summary>
    public required IReadOnlySet<ActionScope> Scopes { get; init; }

    /// <summary>
    ///   Information about the plugin that registered this action.
    /// </summary>
    public required PluginInfo Plugin { get; init; }
}

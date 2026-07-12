using System;
using System.Collections.Generic;
using Shoko.Abstractions.Action.Enums;
using Shoko.Abstractions.Plugin.Models;

namespace Shoko.Abstractions.Action;

/// <summary>
///   Describes an executable action registered by a plugin or the core server.
///   Instances are created by <see cref="Services.IActionService.AddParts"/>
///   and exposed to callers via <see cref="Services.IActionService.GetActions"/>.
/// </summary>
public sealed class ExecutableActionInfo
{
    /// <summary>
    ///   A stable, deterministic identifier for this action derived from its
    ///   implementing type. Suitable for serialization and cross-session use.
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
    ///   The category of the action. <see cref="ActionCategory.PluginInferred"/>
    ///   indicates the display name should be taken from <see cref="CategoryName"/>.
    /// </summary>
    public required ActionCategory Category { get; init; }

    /// <summary>
    ///   The display name for the action's category. When <see cref="Category"/>
    ///   is <see cref="ActionCategory.PluginInferred"/>, this is set to the
    ///   plugin's name. Otherwise it matches the enum member name.
    /// </summary>
    public required string CategoryName { get; init; }

    /// <summary>
    ///   The scopes at which the action can operate. A single action
    ///   implementation may support multiple scopes by implementing
    ///   several of the <c>IExecutable*Action</c> sub-interfaces.
    /// </summary>
    public required IReadOnlySet<ActionScope> Scopes { get; init; }

    /// <summary>
    ///   Indicates whether the action requires explicit user confirmation
    ///   before it can be executed. Destructive or irreversible actions
    ///   should return <c>true</c>.
    /// </summary>
    public required bool RequiresConfirmation { get; init; }

    /// <summary>
    ///   Information about the plugin that the executable action belongs to.
    /// </summary>
    public required LocalPluginInfo PluginInfo { get; init; }
}

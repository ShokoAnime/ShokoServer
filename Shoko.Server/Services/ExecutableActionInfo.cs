using System;
using Shoko.Abstractions.Actions;

namespace Shoko.Server.Services;

/// <summary>
///   Metadata for a registered executable action, cached at registration time
///   in <see cref="ActionService.AddParts"/> so listings never need to
///   resolve an instance.
/// </summary>
/// <param name="Id">
///   The action's stable UUIDv5 identifier, derived from the action type's
///   fully-qualified name namespaced by the owning plugin's ID.
/// </param>
/// <param name="ActionType">
///   The concrete action type, resolved transiently from DI per execution.
/// </param>
/// <param name="Scope">
///   The entity level the action is bound to.
/// </param>
/// <param name="PluginId">
///   The ID of the plugin that owns the action.
/// </param>
/// <param name="Name">
///   The action's display name.
/// </param>
/// <param name="Description">
///   The action's description.
/// </param>
/// <param name="Category">
///   The action's category.
/// </param>
/// <param name="CategoryName">
///   The resolved display name for the category — the owning plugin's name
///   for <see cref="ActionCategory.PluginInferred"/>, otherwise the
///   category's own name.
/// </param>
/// <param name="Permission">
///   The permission required to invoke the action.
/// </param>
/// <param name="RequiresConfirmation">
///   UI hint for destructive actions.
/// </param>
public record ExecutableActionInfo(
    Guid Id,
    Type ActionType,
    ActionScope Scope,
    Guid PluginId,
    string Name,
    string? Description,
    ActionCategory Category,
    string CategoryName,
    ActionPermission Permission,
    bool RequiresConfirmation
);

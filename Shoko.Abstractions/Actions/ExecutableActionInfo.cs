using System;

namespace Shoko.Abstractions.Actions;

/// <summary>
///   Metadata for a registered executable action, as exposed to plugins for
///   listing and invocation. The concrete action type stays internal to the
///   server; plugins work with this metadata and the action's ID only.
/// </summary>
/// <param name="Id">
///   The action's stable UUIDv5 identifier, derived from the action type's
///   fully-qualified name namespaced by the owning plugin's ID.
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
/// <param name="Scope">
///   The entity level the action is bound to.
/// </param>
/// <param name="Permission">
///   The permission required to invoke the action.
/// </param>
/// <param name="RequiresConfirmation">
///   UI hint for destructive actions.
/// </param>
/// <param name="ConfirmationMessage">
///   Optional custom confirmation message for the WebUI prompt.
/// </param>
/// <param name="PluginId">
///   The ID of the plugin that owns the action.
/// </param>
public sealed record ExecutableActionInfo(
    Guid Id,
    string Name,
    string? Description,
    ActionCategory Category,
    string CategoryName,
    ActionScope Scope,
    ActionPermission Permission,
    bool RequiresConfirmation,
    string? ConfirmationMessage,
    Guid PluginId
);

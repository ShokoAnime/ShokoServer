using System;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Actions;

namespace Shoko.Server.API.v3.Models.Action;

/// <summary>
///   A registered executable action, as exposed by the listing endpoints.
/// </summary>
public class ActionInfo
{
    /// <summary>
    ///   The action's stable UUIDv5 identifier.
    /// </summary>
    [Required]
    public Guid ID { get; set; }

    /// <summary>
    ///   The action's display name.
    /// </summary>
    [Required]
    public required string Name { get; set; }

    /// <summary>
    ///   The action's description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///   The action's category.
    /// </summary>
    [Required]
    public ActionCategory Category { get; set; }

    /// <summary>
    ///   The resolved display name for the category — the owning plugin's name
    ///   for <see cref="ActionCategory.PluginInferred"/>, otherwise the
    ///   category's own name.
    /// </summary>
    [Required]
    public required string CategoryName { get; set; }

    /// <summary>
    ///   The entity level the action is bound to.
    /// </summary>
    [Required]
    public ActionScope Scope { get; set; }

    /// <summary>
    ///   The permission required to invoke the action.
    /// </summary>
    [Required]
    public ActionPermission Permission { get; set; }

    /// <summary>
    ///   UI hint for destructive actions. WebUI is expected to prompt before
    ///   invoking the action when this is <see langword="true"/>.
    /// </summary>
    [Required]
    public bool RequiresConfirmation { get; set; }

    /// <summary>
    ///   Optional custom message shown to the user when the WebUI prompts for
    ///   confirmation. When <see langword="null"/>, the WebUI uses a generic
    ///   fallback prompt.
    /// </summary>
    public string? ConfirmationMessage { get; set; }

    /// <summary>
    ///   Whether the action takes invocation parameters, and therefore has a
    ///   parameter form to render before it can be invoked.
    /// </summary>
    /// <remarks>
    ///   Only the flag is carried here. The definition itself is a tree that
    ///   can run to tens of kilobytes, and a listing returns every action the
    ///   caller may invoke, so sending one per row would dwarf the listing.
    /// </remarks>
    [Required]
    public bool HasParameters { get; set; }

    /// <summary>
    ///   Maps a registered action to its API representation.
    /// </summary>
    public static ActionInfo FromExecutableActionInfo(ExecutableActionInfo info) => new()
    {
        ID = info.Id,
        Name = info.Name,
        Description = info.Description,
        Category = info.Category,
        CategoryName = info.CategoryName,
        Scope = info.Scope,
        Permission = info.Permission,
        RequiresConfirmation = info.RequiresConfirmation,
        ConfirmationMessage = info.ConfirmationMessage,
        HasParameters = info.Parameters is not null,
    };
}

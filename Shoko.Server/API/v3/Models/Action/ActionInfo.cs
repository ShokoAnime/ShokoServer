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
    public string Name { get; set; }

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
    public string CategoryName { get; set; }

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
    };
}

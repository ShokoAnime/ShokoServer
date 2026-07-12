using System.Collections.Generic;

namespace Shoko.Server.API.v3.Models.Action;

/// <summary>
///   A group of actions sharing the same category name.
/// </summary>
public sealed class ActionCategoryGroup
{
    /// <summary>
    ///   The display name for the category.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///   The actions in this category.
    /// </summary>
    public required IReadOnlyList<ActionInfo> Actions { get; init; }
}

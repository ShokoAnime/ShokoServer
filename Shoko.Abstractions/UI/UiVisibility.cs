using Shoko.Abstractions.UI.Enums;

namespace Shoko.Abstractions.UI;

/// <summary>
/// Describes when an element is shown and when it is editable.
/// </summary>
public class UiVisibility
{
    /// <summary>
    /// The visibility to use when no condition applies.
    /// </summary>
    public DisplayVisibility Default { get; init; }

    /// <summary>
    /// Whether the element is only shown while the client is in advanced mode.
    /// </summary>
    public bool Advanced { get; init; }

    /// <summary>
    /// A condition that switches the element to another visibility, or
    /// <c>null</c> when the visibility never changes.
    /// </summary>
    public UiVisibilityCondition? Toggle { get; init; }

    /// <summary>
    /// A condition that makes the element read-only, or <c>null</c> when the
    /// element is never conditionally disabled.
    /// </summary>
    public UiCondition? Disable { get; init; }
}

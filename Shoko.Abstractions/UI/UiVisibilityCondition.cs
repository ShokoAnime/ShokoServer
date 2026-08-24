using Shoko.Abstractions.UI.Enums;

namespace Shoko.Abstractions.UI;

/// <summary>
/// A <see cref="UiCondition"/> that switches an element to a different
/// visibility while it holds.
/// </summary>
public class UiVisibilityCondition : UiCondition
{
    /// <summary>
    /// The visibility to apply while the condition holds.
    /// </summary>
    public DisplayVisibility Visibility { get; init; }
}

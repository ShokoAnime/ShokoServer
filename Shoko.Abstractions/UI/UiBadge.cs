using Shoko.Abstractions.UI.Enums;

namespace Shoko.Abstractions.UI;

/// <summary>
/// A small labelled marker rendered next to an element's label.
/// </summary>
public class UiBadge
{
    /// <summary>
    /// The text shown inside the badge.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The colour theme of the badge.
    /// </summary>
    public DisplayColorTheme Theme { get; init; }
}

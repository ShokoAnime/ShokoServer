using Shoko.Abstractions.UI.Enums;

namespace Shoko.Abstractions.UI;

/// <summary>
/// A user-invokable action attached to a section container.
/// </summary>
public class UiAction
{
    /// <summary>
    /// The identifier to send back to the server when the action is invoked.
    /// </summary>
    public string ID { get; init; } = string.Empty;

    /// <summary>
    /// The label of the action's button.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// An optional longer description, usable as a tooltip.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// The colour theme of the action's button.
    /// </summary>
    public DisplayColorTheme Theme { get; init; }

    /// <summary>
    /// Where in the container the action's button belongs.
    /// </summary>
    public DisplayButtonPosition Position { get; init; }

    /// <summary>
    /// The authored size of the action's button.
    /// </summary>
    public DisplayElementSize Size { get; init; }

    /// <summary>
    /// An optional icon name for the action's button.
    /// </summary>
    public string? Icon { get; init; }

    /// <summary>
    /// The section within the container the action belongs to, or <c>null</c>
    /// for the container's default section.
    /// </summary>
    public string? SectionName { get; init; }

    /// <summary>
    /// The member the action is attached to, or <c>null</c> when the action
    /// belongs to the container itself.
    /// </summary>
    public string? MemberName { get; init; }

    /// <summary>
    /// A condition controlling whether the action is shown at all.
    /// </summary>
    public UiCondition? Toggle { get; init; }

    /// <summary>
    /// A condition controlling whether the action is disabled.
    /// </summary>
    public UiCondition? Disable { get; init; }

    /// <summary>
    /// Whether the action is disabled while the configuration is unmodified.
    /// </summary>
    public bool DisableIfNoChanges { get; init; }
}

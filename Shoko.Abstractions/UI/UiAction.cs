using Shoko.Abstractions.UI.Enums;

namespace Shoko.Abstractions.UI;

/// <summary>
/// A user-invokable action attached to a section container.
/// </summary>
/// <remarks>
/// <para>
/// Where the button goes is settled by whatever lists it — a
/// <see cref="UiFloatingSection"/> or the container itself. Listed among that
/// one's start or end actions it is pinned to the top or bottom of it, and
/// listed in its structure it renders inline among the fields.
/// </para>
/// <para>
/// The action carries no path to invoke it at. The same container can be
/// repeated as a list item or a record value, where the path differs per
/// instance, so a client supplies the path of the container instance it
/// rendered the action in.
/// </para>
/// </remarks>
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
    /// The authored size of the action's button.
    /// </summary>
    public DisplayElementSize Size { get; init; }

    /// <summary>
    /// An optional icon name for the action's button.
    /// </summary>
    public string? Icon { get; init; }

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

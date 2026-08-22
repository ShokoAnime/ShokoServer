using Shoko.Abstractions.UI.Enums;

namespace Shoko.Abstractions.UI.Elements;

/// <summary>
/// A selection component whose options are supplied by the server as part of
/// the configuration value rather than by the definition.
/// </summary>
public sealed class UiSelectElement : UiElement
{
    /// <inheritdoc />
    public override UiElementKind Kind => UiElementKind.Select;

    /// <summary>
    /// How the options should be laid out.
    /// </summary>
    public DisplaySelectType SelectType { get; init; }

    /// <summary>
    /// Whether more than one option may be selected at a time.
    /// </summary>
    public bool MultipleItems { get; init; }
}

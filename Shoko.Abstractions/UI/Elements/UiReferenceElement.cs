namespace Shoko.Abstractions.UI.Elements;

/// <summary>
/// A pointer to an element in <see cref="UiDefinition.Definitions"/>, emitted
/// where the element tree would otherwise recurse into itself.
/// </summary>
public sealed class UiReferenceElement : UiElement
{
    /// <inheritdoc />
    public override UiElementKind Kind => UiElementKind.Reference;

    /// <summary>
    /// The key into <see cref="UiDefinition.Definitions"/>.
    /// </summary>
    public string Reference { get; init; } = string.Empty;
}

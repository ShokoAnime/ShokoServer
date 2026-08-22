namespace Shoko.Abstractions.UI.Elements;

/// <summary>
/// A single-line text input.
/// </summary>
public sealed class UiStringElement : UiTextElement
{
    /// <inheritdoc />
    public override UiElementKind Kind => UiElementKind.String;
}

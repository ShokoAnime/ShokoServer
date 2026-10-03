namespace Shoko.Abstractions.UI.Elements;

/// <summary>
/// A multi-line text input.
/// </summary>
public sealed class UiTextAreaElement : UiTextElement
{
    /// <inheritdoc />
    public override UiElementKind Kind => UiElementKind.TextArea;
}

namespace Shoko.Abstractions.UI.Elements;

/// <summary>
/// A masked text input.
/// </summary>
public sealed class UiPasswordElement : UiTextElement
{
    /// <inheritdoc />
    public override UiElementKind Kind => UiElementKind.Password;
}

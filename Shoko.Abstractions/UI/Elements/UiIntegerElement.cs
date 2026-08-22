namespace Shoko.Abstractions.UI.Elements;

/// <summary>
/// A whole-number input.
/// </summary>
public sealed class UiIntegerElement : UiElement
{
    /// <inheritdoc />
    public override UiElementKind Kind => UiElementKind.Integer;

    /// <summary>
    /// The smallest accepted value, or <c>null</c> when unbounded.
    /// </summary>
    public long? Minimum { get; init; }

    /// <summary>
    /// The largest accepted value, or <c>null</c> when unbounded.
    /// </summary>
    public long? Maximum { get; init; }
}

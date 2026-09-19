namespace Shoko.Abstractions.UI.Elements;

/// <summary>
/// A fractional-number input.
/// </summary>
public sealed class UiFloatElement : UiElement
{
    /// <inheritdoc />
    public override UiElementKind Kind => UiElementKind.Float;

    /// <summary>
    /// The smallest accepted value, or <c>null</c> when unbounded.
    /// </summary>
    public double? Minimum { get; init; }

    /// <summary>
    /// The largest accepted value, or <c>null</c> when unbounded.
    /// </summary>
    public double? Maximum { get; init; }
}

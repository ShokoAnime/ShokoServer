namespace Shoko.Abstractions.UI.Elements;

/// <summary>
/// Emitted when the server could not map a schema node onto a known element.
/// Its presence in a definition is a bug report, not a rendering instruction.
/// </summary>
public sealed class UiUnknownElement : UiElement
{
    /// <inheritdoc />
    public override UiElementKind Kind => UiElementKind.Unknown;

    /// <summary>
    /// The JSON schema type the server saw but could not classify.
    /// </summary>
    public string? SchemaType { get; init; }
}

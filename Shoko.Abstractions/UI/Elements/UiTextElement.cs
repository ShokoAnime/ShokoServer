namespace Shoko.Abstractions.UI.Elements;

/// <summary>
/// Shared constraints for every element backed by a JSON string.
/// </summary>
public abstract class UiTextElement : UiElement
{
    /// <summary>
    /// The shortest accepted value, or <c>null</c> when unbounded.
    /// </summary>
    public int? MinLength { get; init; }

    /// <summary>
    /// The longest accepted value, or <c>null</c> when unbounded.
    /// </summary>
    public int? MaxLength { get; init; }

    /// <summary>
    /// A regular expression the value has to match, or <c>null</c>.
    /// </summary>
    public string? Pattern { get; init; }

    /// <summary>
    /// The JSON schema format hint for the value, such as <c>uri</c> or
    /// <c>version</c>, or <c>null</c>.
    /// </summary>
    public string? Format { get; init; }
}

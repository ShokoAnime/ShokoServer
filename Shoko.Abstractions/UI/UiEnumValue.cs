namespace Shoko.Abstractions.UI;

/// <summary>
/// One selectable value of a <see cref="Elements.UiEnumElement"/>.
/// </summary>
public class UiEnumValue
{
    /// <summary>
    /// The human-readable name of the value.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// An optional longer description of the value.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// The value as it appears in the configuration document.
    /// </summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>
    /// The display names of any aliases that collapsed onto this value, or
    /// <c>null</c> when it has none. A renderer may surface these alongside the
    /// title so a value is findable by every name it is known by.
    /// </summary>
    public string? Alias { get; init; }

    /// <summary>
    /// The document values of any aliases that collapsed onto this value, or
    /// <c>null</c> when it has none. A configuration may legitimately be
    /// written with one of these instead of <see cref="Value"/>.
    /// </summary>
    public string? AliasValues { get; init; }
}

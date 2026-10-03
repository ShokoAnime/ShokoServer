using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Stub;

/// <summary>
///   A stub implementation of the <see cref="IText" /> interface.
/// </summary>
public class TextStub : IText
{
    /// <inheritdoc />
    public required MetadataSource Source { get; init; }

    /// <inheritdoc />
    public required TitleLanguage Language { get; init; }

    /// <inheritdoc />
    public required string LanguageCode { get; init; }

    /// <inheritdoc />
    public string? CountryCode { get; init; }

    /// <inheritdoc />
    public required string Value { get; init; }

    /// <inheritdoc />
    public int? ID { get; init; }

    /// <inheritdoc />
    public MetadataGuid? EntityID { get; init; }

    /// <inheritdoc />
    public int? ReferenceID { get; init; }

    /// <inheritdoc />
    public bool IsEnabled { get; init; } = true;

    /// <inheritdoc />
    public TextPreference Preference { get; init; } = TextPreference.None;

    /// <inheritdoc />
    public int Ordering { get; init; }

    /// <inheritdoc />
    public string? ScriptCode { get; init; }

    /// <inheritdoc />
    public bool IsInlineDefault { get; init; }

    /// <inheritdoc />
    public bool Equals(IText? other)
        => IText.Equals(this, other);
}

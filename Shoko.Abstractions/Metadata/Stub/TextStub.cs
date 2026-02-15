using Shoko.Abstractions.Enums;

namespace Shoko.Abstractions.Metadata.Stub;

/// <summary>
///   A stub implementation of the <see cref="IText" /> interface.
/// </summary>
public class TextStub : IText
{
    /// <inheritdoc />
    public required DataSource Source { get; init; }

    /// <inheritdoc />
    public required TitleLanguage Language { get; init; }

    /// <inheritdoc />
    public required string LanguageCode { get; init; }

    /// <inheritdoc />
    public string? CountryCode { get; init; }

    /// <inheritdoc />
    public required string Value { get; init; }

    /// <inheritdoc />
    public bool Equals(IText? other)
        => IText.Equals(this, other);
}

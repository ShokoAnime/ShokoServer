using System;
using Shoko.Abstractions.Enums;

namespace Shoko.Abstractions.Metadata;

/// <summary>
///   Represents a text from a data source.
/// </summary>
public interface IText : IMetadata, IEquatable<IText>
{
    /// <summary>
    ///   The language enum inferred from the language code and country code.
    /// </summary>
    TitleLanguage Language { get; }

    /// <summary>
    /// Alpha 2, alpha 3 or an 'x-' prefixed custom language code, or 'unk' if
    /// unknown.
    /// </summary>
    string LanguageCode { get; }

    /// <summary>
    /// Alpha 2 or alpha 3 country code, or <c>null</c> if unknown.
    /// </summary>
    string? CountryCode { get; }

    /// <summary>
    /// The value.
    /// </summary>
    string Value { get; }

    /// <summary>
    ///   Checks if two text objects are equal.
    /// </summary>
    /// <param name="textA">
    ///   The first text.
    /// </param>
    /// <param name="textB">
    ///   The second text.
    /// </param>
    /// <returns>
    ///   <c>true</c> if the texts are equal; otherwise, <c>false</c>.
    /// </returns>
    public static bool Equals(IText? textA, IText? textB)
        => textA is not null && textB is not null && (
            ReferenceEquals(textA, textB) || (
                textA.Source == textB.Source &&
                textA.Language == textB.Language &&
                string.Equals(textA.CountryCode, textB.CountryCode) &&
                string.Equals(textA.Value, textB.Value)
            )
        );
}

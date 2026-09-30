using System;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata;

/// <summary>
///   Represents a text from a metadata source.
/// </summary>
public interface IText : IEquatable<IText>
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
    ///   The source of the text.
    /// </summary>
    MetadataSource Source { get; }

    #region Stored Text

    /// <summary>
    ///   The stored text's ID, unique among texts of its kind.
    /// </summary>
    /// <value>
    ///   The ID, or <c>null</c> for a text that is not stored, such as one a
    ///   source keeps on its own entity row or one built on the fly.
    /// </value>
    int? ID { get => null; }

    /// <summary>
    ///   The entry the stored text belongs to.
    /// </summary>
    /// <value>
    ///   The entry, or <c>null</c> for a text that is not stored.
    /// </value>
    MetadataGuid? EntityID { get => null; }

    /// <summary>
    ///   The stored text a user's pick copies, which keeps the copy's value
    ///   in step and removes the copy along with it.
    /// </summary>
    /// <value>
    ///   The ID of the picked text, of the same kind, or <c>null</c> when the
    ///   text is not a pick of another stored text.
    /// </value>
    int? ReferenceID { get => null; }

    /// <summary>
    ///   Whether the text may be listed and chosen. A disabled text is kept,
    ///   so a refresh does not bring it back.
    /// </summary>
    /// <value>
    ///   <c>false</c> when a user disabled the text; otherwise <c>true</c>.
    /// </value>
    bool IsEnabled { get => true; }

    /// <summary>
    ///   How strongly a user prefers the text for its entry.
    /// </summary>
    /// <value>
    ///   <see cref="TextPreference.Overall"/> beats every other text,
    ///   <see cref="TextPreference.Language"/> wins for the text's own
    ///   language, and <see cref="TextPreference.None"/> leaves the choice to
    ///   the language and source orders.
    /// </value>
    TextPreference Preference { get => TextPreference.None; }

    /// <summary>
    ///   Where the text sits among the texts of the same kind its source gave
    ///   the entry.
    /// </summary>
    /// <value>
    ///   The position, from <c>0</c>, as the source listed it.
    /// </value>
    int Ordering { get => 0; }

    /// <summary>
    ///   The script the text is written in, as an ISO 15924 code.
    /// </summary>
    /// <value>
    ///   The code, such as <c>Latn</c> or <c>Hans</c>, or <c>null</c> when the
    ///   source did not say.
    /// </value>
    string? ScriptCode { get => null; }

    /// <summary>
    ///   Whether the text is the source's own default, kept on the entry's own
    ///   row rather than stored among the entry's texts.
    /// </summary>
    /// <value>
    ///   <c>true</c> for such a default, which can be overridden by a
    ///   preference but never disabled or removed; otherwise <c>false</c>.
    /// </value>
    bool IsInlineDefault { get => false; }

    #endregion

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

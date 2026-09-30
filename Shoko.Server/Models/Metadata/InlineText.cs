using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Server.Models.Interfaces;

namespace Shoko.Server.Models.Metadata;

/// <summary>
///   Builds the defaults an entry keeps on its own row, as
///   <see cref="IInlineTextSource"/> hands them to the text manager.
/// </summary>
internal static class InlineText
{
    /// <summary>
    ///   A default title from an entry's row.
    /// </summary>
    /// <param name="source">The entry's source.</param>
    /// <param name="value">The title, which may be missing.</param>
    /// <param name="language">The language the entry has always given it.</param>
    /// <param name="languageCode">The language code.</param>
    /// <param name="countryCode">The country code, when there is one.</param>
    /// <param name="type">The title's type; the source's main title unless the source says otherwise.</param>
    /// <returns>The title, or <c>null</c> when the row has none.</returns>
    internal static ITitle? Title(MetadataSource source, string? value, TitleLanguage language, string languageCode, string? countryCode = null, TitleType type = TitleType.Main)
        => string.IsNullOrEmpty(value)
            ? null
            : new InlineTitle { Source = source, Value = value, Language = language, LanguageCode = languageCode, CountryCode = countryCode, Type = type };

    /// <summary>
    ///   A default overview from an entry's row.
    /// </summary>
    /// <param name="source">The entry's source.</param>
    /// <param name="value">The overview, which may be missing.</param>
    /// <param name="language">The language the entry has always given it.</param>
    /// <param name="languageCode">The language code.</param>
    /// <param name="countryCode">The country code, when there is one.</param>
    /// <returns>The overview, or <c>null</c> when the row has none.</returns>
    internal static IText? Overview(MetadataSource source, string? value, TitleLanguage language, string languageCode, string? countryCode = null)
        => string.IsNullOrEmpty(value)
            ? null
            : new InlineOverview { Source = source, Value = value, Language = language, LanguageCode = languageCode, CountryCode = countryCode };

    /// <summary>
    ///   A default title kept on an entry's row.
    /// </summary>
    private sealed class InlineTitle : TitleStub, ITitle
    {
        bool IText.IsInlineDefault => true;
    }

    /// <summary>
    ///   A default overview kept on an entry's row.
    /// </summary>
    private sealed class InlineOverview : TextStub, IText
    {
        bool IText.IsInlineDefault => true;
    }
}

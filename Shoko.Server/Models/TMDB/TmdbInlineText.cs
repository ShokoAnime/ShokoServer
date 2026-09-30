using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Models.Metadata;

namespace Shoko.Server.Models.TMDB;

/// <summary>
///   Builds the English title and overview a TMDB entry keeps on its row,
///   which TMDB gives as American English.
/// </summary>
internal static class TmdbInlineText
{
    /// <summary>
    ///   The English title on a TMDB entry's row, an official title like
    ///   every other title TMDB lists.
    /// </summary>
    /// <param name="value">The title, which may be missing.</param>
    /// <returns>The title, or <c>null</c> when the row has none.</returns>
    internal static ITitle? Title(string? value)
        => InlineText.Title(MetadataSource.TMDB, value, TitleLanguage.EnglishAmerican, "en", "US", TitleType.Official);

    /// <summary>
    ///   The English overview on a TMDB entry's row.
    /// </summary>
    /// <param name="value">The overview, which may be missing.</param>
    /// <returns>The overview, or <c>null</c> when the row has none.</returns>
    internal static IText? Overview(string? value)
        => InlineText.Overview(MetadataSource.TMDB, value, TitleLanguage.EnglishAmerican, "en", "US");

    /// <summary>
    ///   The English title on a TMDB entry's row, even an empty one, which a
    ///   TMDB entry falls back to when no title is in a preferred language.
    /// </summary>
    /// <param name="value">The title, which may be empty.</param>
    /// <returns>The title.</returns>
    internal static ITitle TitleOrEmpty(string? value)
        => Title(value) ?? new TitleStub
        {
            Source = MetadataSource.TMDB,
            Language = TitleLanguage.EnglishAmerican,
            LanguageCode = "en",
            CountryCode = "US",
            Value = string.Empty,
            Type = TitleType.Official,
        };

    /// <summary>
    ///   The English overview on a TMDB entry's row, even an empty one, which
    ///   a TMDB entry falls back to when no overview is in a preferred
    ///   language.
    /// </summary>
    /// <param name="value">The overview, which may be empty.</param>
    /// <returns>The overview.</returns>
    internal static IText OverviewOrEmpty(string? value)
        => Overview(value) ?? new TextStub
        {
            Source = MetadataSource.TMDB,
            Language = TitleLanguage.EnglishAmerican,
            LanguageCode = "en",
            CountryCode = "US",
            Value = string.Empty,
        };

    /// <summary>
    ///   Where a TMDB entry lists the English text on its row.
    /// </summary>
    /// <param name="listed">Whether TMDB listed it among the entry's translations.</param>
    /// <returns>Where its stored texts left room for it, or nowhere.</returns>
    internal static InlineTextPlacement Placement(bool listed)
        => listed ? InlineTextPlacement.InGap : InlineTextPlacement.Unlisted;
}

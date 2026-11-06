using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata;

/// <summary>
///   How <see cref="Services.IMetadataService"/> merges an anime's genres
///   and studios across its sources.
/// </summary>
public sealed record MetadataMergeOptions
{
    /// <summary>
    ///   The sources to use, highest ranked first. A source not listed is not
    ///   used, except AniDB, which is always used: at its place when listed,
    ///   else first. Duplicates count once.
    /// </summary>
    public IReadOnlyList<MetadataSource> SourceOrder { get; init; } = [MetadataSource.TMDB];

    /// <summary>
    ///   The most genres to give for an anime.
    /// </summary>
    public int GenreLimit { get; init; } = 5;

    /// <summary>
    ///   The genres to leave out, such as those that only say "anime", matched
    ///   by name ignoring case, spacing, punctuation and diacritics.
    /// </summary>
    public IReadOnlyList<string> ExcludedGenres { get; init; } = ["anime", "animation"];
}

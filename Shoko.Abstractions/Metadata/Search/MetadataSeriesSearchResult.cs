using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Search;

/// <summary>
///   A series a source offered for a search.
/// </summary>
/// <remarks>
///   Placed by when it started airing, unlike a
///   <see cref="MetadataMovieSearchResult"/>, placed by when it came out.
/// </remarks>
public sealed record MetadataSeriesSearchResult : MetadataSearchResult
{
    /// <summary>
    ///   When it first aired, as far as the source knows.
    /// </summary>
    public PartialDateOnly? FirstAiredAt { get; init; }

    /// <summary>
    ///   What kind of release it is, where the source says. Sources that only
    ///   carry television leave this at <see cref="AnimeType.TV"/>.
    /// </summary>
    public AnimeType Type { get; init; } = AnimeType.TV;

    /// <summary>
    ///   The broadcast season it belongs to, where the source says.
    /// </summary>
    public YearlySeason? Season { get; init; }

    /// <summary>
    ///   The year that season falls in.
    /// </summary>
    public int? SeasonYear { get; init; }

    /// <summary>
    ///   How many episodes the source says it has; weighs heavily when
    ///   choosing between candidates.
    /// </summary>
    public int? EpisodeCount { get; init; }

    /// <summary>
    ///   Its regular seasons, specials left out, or <c>null</c>
    ///   where the source does not group episodes into seasons or the search
    ///   did not say.
    /// </summary>
    /// <remarks>
    ///   An empty list is a source saying the series has no seasons yet,
    ///   which is not the same as it not saying.
    /// </remarks>
    public IReadOnlyList<MetadataSearchResultSeason>? Seasons { get; init; }
}

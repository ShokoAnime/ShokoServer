using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Anilist;

/// <summary>
/// Options for searching AniList for anime. Every filter maps onto a filter
/// AniList applies server-side, so narrowing here costs no extra requests.
/// </summary>
public sealed class AnilistSearchOptions
{
    /// <summary>
    /// The title to search for.
    /// </summary>
    public required string Query { get; set; }

    /// <summary>
    /// Include restricted (adult) anime in the results.
    /// </summary>
    public bool IncludeRestricted { get; set; }

    /// <summary>
    /// Only include anime that started airing in this year, if set.
    /// </summary>
    public int? Year { get; set; }

    /// <summary>
    /// Only include anime released in this season, if set. Combine with
    /// <see cref="SeasonYear"/> to pin a specific yearly season.
    /// </summary>
    public YearlySeason? Season { get; set; }

    /// <summary>
    /// Only include anime released in this season year, if set. Unlike
    /// <see cref="Year"/>, this matches AniList's own season year, which can
    /// differ from the start date for late-December premieres.
    /// </summary>
    public int? SeasonYear { get; set; }

    /// <summary>
    /// Only include anime of these types, if set and non-empty.
    /// </summary>
    public IReadOnlyList<AnimeType>? Types { get; set; }

    /// <summary>
    /// The page to return, starting at 1.
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// The number of results per page. AniList caps this at 50.
    /// </summary>
    public int PageSize { get; set; } = 6;
}

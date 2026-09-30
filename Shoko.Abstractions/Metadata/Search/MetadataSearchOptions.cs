using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Search;

/// <summary>
///   What to search a source for.
/// </summary>
/// <remarks>
///   A source ignores what it cannot narrow by rather than refusing the
///   search, so a caller can fill in everything it knows and let each source
///   take what it can use.
/// </remarks>
public sealed record MetadataSearchOptions
{
    /// <summary>
    ///   What to search for.
    /// </summary>
    public required string Query { get; init; }

    /// <summary>
    ///   Whether to let adult entries through.
    /// </summary>
    public bool IncludeRestricted { get; init; }

    /// <summary>
    ///   Limit to entries from one year.
    /// </summary>
    public int? Year { get; init; }

    /// <summary>
    ///   Limit to entries from one broadcast season. Ignored by a source that
    ///   does not group by season.
    /// </summary>
    public YearlySeason? Season { get; init; }

    /// <summary>
    ///   The year <see cref="Season"/> falls in, where it differs from
    ///   <see cref="Year"/>.
    /// </summary>
    public int? SeasonYear { get; init; }

    /// <summary>
    ///   Limit to certain kinds of release. Only meaningful when searching for
    ///   series, a film being one kind already.
    /// </summary>
    public IReadOnlyList<AnimeType>? Types { get; init; }

    /// <summary>
    ///   Which page of results to fetch, counting from one.
    /// </summary>
    public int Page { get; init; } = 1;

    /// <summary>
    ///   How many results to a page.
    /// </summary>
    public int PageSize { get; init; } = 6;
}

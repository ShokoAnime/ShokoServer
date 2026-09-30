using System;
using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata.Search;

/// <summary>
///   A film a source offered for a search.
/// </summary>
/// <remarks>
///   Placed by when it came out, unlike a
///   <see cref="MetadataSeriesSearchResult"/>, placed by when it started airing.
/// </remarks>
public sealed record MetadataMovieSearchResult : MetadataSearchResult
{
    /// <summary>
    ///   When it was released, as far as the source knows.
    /// </summary>
    public PartialDateOnly? ReleasedAt { get; init; }

    /// <summary>
    ///   Every other date it came out on, such as in another country or on
    ///   home video, where the source lists them.
    /// </summary>
    public IReadOnlyList<DateOnly> OtherReleaseDates { get; init; } = [];

    /// <summary>
    ///   Whether the source marks it as a standalone video rather than a film
    ///   proper.
    /// </summary>
    public bool IsStandaloneVideo { get; init; }
}

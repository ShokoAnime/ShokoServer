using System;
using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata.Search;

/// <summary>
///   One season of a series a source offered, as far as a search tells.
/// </summary>
/// <remarks>
///   Enough to tell which season an anime is, which is what matching needs
///   from a source that keeps a whole show under one entry.
/// </remarks>
public sealed record MetadataSearchResultSeason
{
    /// <summary>
    ///   The season's number at the source.
    /// </summary>
    public required int SeasonNumber { get; init; }

    /// <summary>
    ///   How many episodes the source says it has.
    /// </summary>
    public int? EpisodeCount { get; init; }

    /// <summary>
    ///   When the season started airing, as far as the source knows.
    /// </summary>
    public PartialDateOnly? FirstAiredAt { get; init; }

    /// <summary>
    ///   When its first episode aired, where the source has fetched it.
    /// </summary>
    /// <remarks>
    ///   Apart from <see cref="FirstAiredAt"/>, since a source may only know it
    ///   after asking for the season on its own.
    /// </remarks>
    public DateOnly? FirstEpisodeAiredAt { get; init; }

    /// <summary>
    ///   The season's regular episodes with their air dates, or
    ///   <c>null</c> when the source did not send them.
    /// </summary>
    /// <remarks>
    ///   Matching lines their dates up with the anime's, telling a split cour
    ///   or a remake apart. They cost a remote call per season, so send them
    ///   for your few best candidates only (every season, for one you have
    ///   stored). A source without seasons may send them as one season.
    /// </remarks>
    public IReadOnlyList<MetadataSearchResultEpisode>? Episodes { get; init; }
}

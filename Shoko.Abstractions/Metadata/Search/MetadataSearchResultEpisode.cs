using System;

namespace Shoko.Abstractions.Metadata.Search;

/// <summary>
///   One episode of a season a source offered, as light as matching needs.
/// </summary>
/// <remarks>
///   Not an <see cref="IEpisode"/>: nothing is stored for it. The matching
///   engine lines the dates up with the anime's episodes to tell which season
///   the anime is and where in it the anime starts.
/// </remarks>
public sealed record MetadataSearchResultEpisode
{
    /// <summary>
    ///   The episode's number within its season at the source.
    /// </summary>
    public required int EpisodeNumber { get; init; }

    /// <summary>
    ///   When it aired, or <see langword="null"/> when the source does not
    ///   say.
    /// </summary>
    public DateOnly? AiredAt { get; init; }

    /// <summary>
    ///   Its title, where the source gives one.
    /// </summary>
    public string? Title { get; init; }
}

namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   What a next-only airing read keeps one airing per. The parts combine, so
///   <see cref="Series"/> with <see cref="Channel"/> keeps the next airing of
///   each series on each of its channels.
/// </summary>
public enum AiringNextGrouping
{
    /// <summary>
    ///   One next airing per series: the series of the episode the airing was
    ///   resolved for.
    /// </summary>
    Series = 0,

    /// <summary>
    ///   One next airing per channel. The airings with no channel share one
    ///   group.
    /// </summary>
    Channel = 1,

    /// <summary>
    ///   One next airing per track kind. An airing whose schedule releases
    ///   several kinds counts for each of them, and a date-only entry counts
    ///   as <see cref="AiringKind.Original"/>.
    /// </summary>
    Kind = 2,
}

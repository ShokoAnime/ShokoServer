namespace Shoko.Abstractions.Metadata;

/// <summary>
///   Where one source places a Shoko special among the regular episodes of
///   its series, read from the default ordering of the source's own series
///   and named by the series' Shoko episodes.
/// </summary>
/// <remarks>
///   A view built when asked for, never stored. It holds IDs only.
/// </remarks>
public interface IEpisodePlacement
{
    /// <summary>
    ///   The source placing the special: <c>anidb</c> for its AniDB titles,
    ///   or a linked plugin source.
    /// </summary>
    MetadataSource Source { get; }

    /// <summary>
    ///   The Shoko episode placed, as <c>shoko://episode/&lt;id&gt;</c>.
    /// </summary>
    MetadataGuid EpisodeID { get; }

    /// <summary>
    ///   The source's episode whose place this is: the AniDB episode, or the
    ///   linked episode of the plugin source.
    /// </summary>
    MetadataGuid SourceEpisodeID { get; }

    /// <summary>
    ///   The Shoko episode the special airs right after, or <c>null</c> when
    ///   it airs first.
    /// </summary>
    MetadataGuid? AirsAfterEpisodeID { get; }

    /// <summary>
    ///   The Shoko episode the special airs right before, or <c>null</c> when
    ///   it airs last.
    /// </summary>
    MetadataGuid? AirsBeforeEpisodeID { get; }
}

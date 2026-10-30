namespace Shoko.Abstractions.Metadata.CrossReferences;

/// <summary>
///   A whole anime that is one of your films. It is kept against the episode
///   standing for the film, so it answers at both levels.
/// </summary>
public sealed record MetadataMovieLinkData : MetadataLinkData
{
    /// <summary>
    ///   The AniDB episode the film stands for.
    /// </summary>
    public required int AnidbEpisodeID { get; init; }
}

namespace Shoko.Abstractions.Metadata.CrossReferences;

/// <summary>
///   One AniDB episode that is one of your episodes.
/// </summary>
public sealed record MetadataEpisodeLinkData : MetadataLinkData
{
    /// <summary>
    ///   The AniDB episode being linked.
    /// </summary>
    public required int AnidbEpisodeID { get; init; }

    /// <summary>
    ///   The series on <see cref="MetadataLinkData.Source"/> the episode
    ///   belongs to, when known.
    /// </summary>
    public MetadataGuid? ProviderParentID { get; init; }

    /// <summary>
    ///   The season on <see cref="MetadataLinkData.Source"/> the episode sits
    ///   in, when known. Left out, the store fills it from the core's series
    ///   store when the episode is there. Not kept for a core source, whose
    ///   links read the season and numbers from its own tables.
    /// </summary>
    /// <remarks>
    ///   The link counts toward a season link only when the season's number
    ///   and <see cref="ProviderParentID"/> are known too, from the link or
    ///   from the series store. A source with nothing in the store passes all
    ///   three.
    /// </remarks>
    public MetadataGuid? SeasonID { get; init; }

    /// <summary>
    ///   The number of the season the episode sits in, when known. Left out,
    ///   it is filled the same way as <see cref="SeasonID"/>.
    /// </summary>
    public int? SeasonNumber { get; init; }

    /// <summary>
    ///   The episode's number within its season, when known. Left out, it is
    ///   filled the same way as <see cref="SeasonID"/>.
    /// </summary>
    public int? EpisodeNumber { get; init; }
}

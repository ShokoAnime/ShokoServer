using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.CrossReferences;

/// <summary>
///   What every link to store carries, whichever level it is made at. Where it
///   sits among an entry's links is the store's to assign.
/// </summary>
public abstract record MetadataLinkData
{
    /// <summary>
    ///   The source the linked entry belongs to.
    /// </summary>
    public required MetadataSource Source { get; init; }

    /// <summary>
    ///   The AniDB anime, which every link belongs to whatever its level.
    /// </summary>
    public required int AnidbAnimeID { get; init; }

    /// <summary>
    ///   The entry on <see cref="Source"/>, of the kind the link's level
    ///   names. Only an episode link may leave it <c>null</c>, to mark the
    ///   AniDB episode as deliberately not on your source, so matching leaves
    ///   it alone; a series or film link must name an entry.
    /// </summary>
    public required MetadataGuid? ProviderID { get; init; }

    /// <summary>
    ///   How the link was arrived at. A manual one is
    ///   <see cref="Enums.MatchRating.UserVerified"/>.
    /// </summary>
    public MatchRating MatchRating { get; init; } = MatchRating.UserVerified;
}

/// <summary>
///   A whole anime that is the same work as one of your series, or as one of
///   your films claiming the whole anime. Its
///   <see cref="MetadataLinkData.ProviderID"/> names a series or a film, and
///   reads back as the kind it was written as.
/// </summary>
public sealed record MetadataSeriesLinkData : MetadataLinkData;

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
    ///   store when the episode is there. Not kept for a core source such as
    ///   TMDB, whose links read the season and numbers from its own tables.
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

using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Providers;

/// <summary>
///   A link between one AniDB episode and a provider's entry, which is either
///   an episode or the film the episode stands for.
/// </summary>
/// <remarks>
///   Kept apart from <see cref="MetadataSeriesLinkRequest"/> because the two
///   are keyed on different things: this one names an episode.
/// </remarks>
public sealed record MetadataEpisodeLinkRequest
{
    /// <summary>
    ///   The source the provider entry belongs to.
    /// </summary>
    public required MetadataSource Source { get; init; }

    /// <summary>
    ///   What kind of entry <see cref="ProviderID"/> names: an episode, or the
    ///   film this episode stands for.
    /// </summary>
    public required MetadataEntityType EntityType { get; init; }

    /// <summary>
    ///   The provider entry, on <see cref="Source"/> and of the kind
    ///   <see cref="EntityType"/> names, or <c>null</c> to record the AniDB
    ///   episode as deliberately on no episode of the source. A film link
    ///   must name a film.
    /// </summary>
    public required MetadataGuid? ProviderID { get; init; }

    /// <summary>
    ///   The provider series the episode sits in, on <see cref="Source"/>,
    ///   for an episode the core's series store does not hold yet.
    /// </summary>
    /// <remarks>
    ///   Nothing is refreshed: the caller queues a refresh of the series so the
    ///   link gets its season and numbers. An episode the store holds names its
    ///   own series, which wins; a film has none.
    /// </remarks>
    public MetadataGuid? ProviderSeriesID { get; init; }

    /// <summary>
    ///   The AniDB episode being linked.
    /// </summary>
    public required int AnidbEpisodeID { get; init; }

    /// <summary>
    ///   The AniDB anime the episode belongs to, since a link is stored against
    ///   both.
    /// </summary>
    public required int AnidbAnimeID { get; init; }

    /// <summary>
    ///   Whether to keep the links already there.
    /// </summary>
    public bool Additive { get; init; } = true;

    /// <summary>
    ///   How the link was arrived at. A manual one is
    ///   <see cref="MatchRating.UserVerified"/>, which is why that is the
    ///   default.
    /// </summary>
    public MatchRating MatchRating { get; init; } = MatchRating.UserVerified;

    /// <summary>
    ///   Where this link sits when the episode carries several.
    /// </summary>
    public int? Ordering { get; init; }

    /// <summary>
    ///   On removal, whether to discard the provider's cached data for the
    ///   entry rather than only breaking the link.
    /// </summary>
    public bool Purge { get; init; }

    /// <summary>
    ///   On removal, whether to also tell the source to leave the episode's anime
    ///   alone, as a person taking a link away usually means. Automatic
    ///   linking then no longer links it on its own.
    /// </summary>
    public bool DisableAutoLinking { get; init; }
}

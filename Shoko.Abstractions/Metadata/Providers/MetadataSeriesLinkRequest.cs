using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Providers;

/// <summary>
///   A link claiming a whole anime is the same work as a provider's entry.
/// </summary>
/// <remarks>
///   Kept apart from <see cref="MetadataEpisodeLinkRequest"/> because the two
///   are keyed on different things: this one names an anime and claims all
///   of it. Writing it queues no refresh of the entry.
/// </remarks>
public sealed record MetadataSeriesLinkRequest
{
    /// <summary>
    ///   The source the provider entry belongs to.
    /// </summary>
    public required MetadataSource Source { get; init; }

    /// <summary>
    ///   What kind of entry <see cref="ProviderID"/> names: a series, or a
    ///   film claiming the whole anime. A film's claim is kept as a
    ///   series-level link, which reads back naming the film.
    /// </summary>
    public required MetadataEntityType EntityType { get; init; }

    /// <summary>
    ///   The provider entry, on <see cref="Source"/> and of the kind
    ///   <see cref="EntityType"/> names. A link must name one; <c>null</c>
    ///   only matches an old link to nothing on removal.
    /// </summary>
    public required MetadataGuid? ProviderID { get; init; }

    /// <summary>
    ///   The AniDB anime being linked.
    /// </summary>
    public required int AnidbAnimeID { get; init; }

    /// <summary>
    ///   Whether to keep the links already there. Replacing them is the usual
    ///   intent when a user corrects a wrong one.
    /// </summary>
    public bool Additive { get; init; } = true;

    /// <summary>
    ///   How the link was arrived at. A manual one is
    ///   <see cref="MatchRating.UserVerified"/>, which is why that is the
    ///   default.
    /// </summary>
    public MatchRating MatchRating { get; init; } = MatchRating.UserVerified;

    /// <summary>
    ///   On removal, whether to discard the provider's cached data for the
    ///   entry rather than only breaking the link.
    /// </summary>
    public bool Purge { get; init; }

    /// <summary>
    ///   On removal, whether to also tell the source to leave the anime
    ///   alone, as a person taking a link away usually means. Automatic
    ///   linking then no longer links it on its own.
    /// </summary>
    public bool DisableAutoLinking { get; init; }
}

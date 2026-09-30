using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Events;

/// <summary>
///   One link a write changed.
/// </summary>
public sealed record MetadataLinkChange
{
    /// <summary>
    ///   What became of the link.
    /// </summary>
    public required MetadataLinkChangeKind Kind { get; init; }

    /// <summary>
    ///   The source the link points at.
    /// </summary>
    public required MetadataSource Source { get; init; }

    /// <summary>
    ///   The level the link is made at: <see cref="MetadataEntityType.Series"/>
    ///   for a whole anime, a film claiming one included,
    ///   <see cref="MetadataEntityType.Movie"/> for a film one episode stands
    ///   for, or <see cref="MetadataEntityType.Episode"/>.
    /// </summary>
    public required MetadataEntityType EntityType { get; init; }

    /// <summary>
    ///   The AniDB anime the link belongs to.
    /// </summary>
    public required int AnidbAnimeID { get; init; }

    /// <summary>
    ///   The AniDB episode of a film or episode link, or
    ///   <see langword="null"/> for a link of the whole anime.
    /// </summary>
    public int? AnidbEpisodeID { get; init; }

    /// <summary>
    ///   The entry the link names: the new one when it was replaced, the one
    ///   it named when it was removed. <see langword="null"/> for a link
    ///   deliberately naming no entry.
    /// </summary>
    public MetadataGuid? ProviderID { get; init; }

    /// <summary>
    ///   The entry a replaced link named before, or <see langword="null"/>
    ///   for any other change or a link that named no entry.
    /// </summary>
    public MetadataGuid? PreviousProviderID { get; init; }

    /// <summary>
    ///   The link's rating after the write, or <see langword="null"/> when it
    ///   was removed.
    /// </summary>
    public MatchRating? MatchRating { get; init; }

    /// <summary>
    ///   The link's rating before the write, or <see langword="null"/> when it
    ///   was added.
    /// </summary>
    public MatchRating? PreviousMatchRating { get; init; }
}

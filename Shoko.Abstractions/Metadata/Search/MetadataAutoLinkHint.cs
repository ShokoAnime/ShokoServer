using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata.Search;

/// <summary>
///   An entry of one source that the anime's links on other sources name as
///   the anime's own.
/// </summary>
/// <remarks>
///   Read from the cross-source IDs of the entries the anime is linked to, so
///   an auto-linker can rate the entry and hand it back as a
///   <see cref="Enums.MetadataAutoLinkOrigin.CrossSourceLink"/> candidate.
/// </remarks>
public sealed record MetadataAutoLinkHint
{
    /// <summary>
    ///   The entry named: a series or a film, or an episode when the core
    ///   could not tell which series it belongs to because it is not stored.
    /// </summary>
    public required MetadataGuid ID { get; init; }

    /// <summary>
    ///   The AniDB anime whose links named it.
    /// </summary>
    public required int AnidbAnimeID { get; init; }

    /// <summary>
    ///   The AniDB episode a named film stands for, when the link naming it
    ///   was one for that episode, or <see langword="null"/> for the whole
    ///   anime.
    /// </summary>
    public int? AnidbEpisodeID { get; init; }

    /// <summary>
    ///   The linked entries of other sources that name it, in the order the
    ///   anime's links were read: series links, then film links, then
    ///   episode links.
    /// </summary>
    public required IReadOnlyList<MetadataGuid> NamedBy { get; init; }
}

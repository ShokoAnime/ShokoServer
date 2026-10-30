using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata.Orderings;

/// <summary>
///   What an ordering import did with one ordering of the payload.
/// </summary>
public sealed record MetadataOrderingImportEntry
{
    /// <summary>
    ///   The ordering's place in the payload, from 0.
    /// </summary>
    public required int Index { get; init; }

    /// <summary>
    ///   The ordering's name in the payload.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///   The name it was stored under, which differs from <see cref="Name"/>
    ///   when both were kept, or <c>null</c> when it was skipped.
    /// </summary>
    public string? StoredName { get; init; }

    /// <summary>
    ///   The AniDB anime the ordering is for.
    /// </summary>
    public required int AnidbAnimeID { get; init; }

    /// <summary>
    ///   The Shoko series it was imported into, or <c>null</c> when the anime
    ///   is not in the collection.
    /// </summary>
    public MetadataGuid? SeriesID { get; init; }

    /// <summary>
    ///   The local ordering made or replaced, or <c>null</c> when it was
    ///   skipped or on a dry run that would make a new one.
    /// </summary>
    public MetadataGuid? OrderingID { get; init; }

    /// <summary>
    ///   What was done.
    /// </summary>
    public required MetadataOrderingImportOutcome Outcome { get; init; }

    /// <summary>
    ///   Why it was skipped, or <c>null</c>.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    ///   Whether it was chosen as its series' ordering.
    /// </summary>
    public bool IsPreferred { get; init; }

    /// <summary>
    ///   The episodes dropped from their groups because they could not be
    ///   found in the series.
    /// </summary>
    public IReadOnlyList<MetadataOrderingUnresolvedEpisode> UnresolvedEpisodes { get; init; } = [];

    /// <summary>
    ///   The networks the ordering was linked to, or on a dry run would be,
    ///   in order.
    /// </summary>
    public IReadOnlyList<MetadataGuid> Networks { get; init; } = [];

    /// <summary>
    ///   The networks of <see cref="Networks"/> this server did not have,
    ///   kept as stubs with an empty name until their source saves them, or
    ///   on a dry run that would be.
    /// </summary>
    public IReadOnlyList<MetadataGuid> StubbedNetworks { get; init; } = [];

    /// <summary>
    ///   What was done with each image of the ordering and its groups.
    /// </summary>
    public IReadOnlyList<MetadataOrderingImageImportEntry> Images { get; init; } = [];

    /// <summary>
    ///   What else was changed on the way, such as a second special group
    ///   made a regular one.
    /// </summary>
    public IReadOnlyList<string> Notes { get; init; } = [];
}

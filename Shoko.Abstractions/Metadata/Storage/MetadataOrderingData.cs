using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Storage;

/// <summary>
///   A global ordering to store, whole, under the caller's own source, with
///   the IDs the caller gives it and each of its groups. Saving it replaces
///   what was stored for the ordering.
/// </summary>
public sealed record MetadataOrderingData
{
    /// <summary>
    ///   The ordering: the caller's source, the <c>ordering</c> kind and the
    ///   caller's own ID for it, e.g. <c>anilist://ordering/21-dvd</c>. An ID
    ///   may not start with <c>default/</c>, which the default orderings use.
    /// </summary>
    public required MetadataGuid ID { get; init; }

    /// <summary>
    ///   The series the ordering orders, on any source.
    /// </summary>
    public required MetadataGuid SeriesID { get; init; }

    /// <summary>
    ///   The ordering's name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///   What the ordering is about, if anything.
    /// </summary>
    public string? Overview { get; init; }

    /// <summary>
    ///   What the ordering follows. Neither <see cref="OrderingType.Default"/>
    ///   nor <see cref="OrderingType.User"/>, which the core keeps for the
    ///   default orderings and the users' own.
    /// </summary>
    public OrderingType Type { get; init; } = OrderingType.Unknown;

    /// <summary>
    ///   The ordering's groups, in viewing order.
    /// </summary>
    public IReadOnlyList<MetadataOrderingGroupData> Groups { get; init; } = [];
}

/// <summary>
///   One group of a global ordering, read back as a season of the ordering.
/// </summary>
public sealed record MetadataOrderingGroupData
{
    /// <summary>
    ///   The group: the ordering's source, the <c>season</c> kind and the
    ///   caller's own ID for it, unique among the source's groups and apart
    ///   from its own seasons' IDs.
    /// </summary>
    public required MetadataGuid ID { get; init; }

    /// <summary>
    ///   The group's name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///   What the group is about, if anything.
    /// </summary>
    public string? Overview { get; init; }

    /// <summary>
    ///   Whether the group holds the ordering's specials. It reads back as
    ///   season <c>0</c> wherever it sits, and the other groups are numbered
    ///   by their place from <c>1</c>. At most one group of an ordering.
    /// </summary>
    public bool IsSpecial { get; init; }

    /// <summary>
    ///   The group's episodes, in viewing order. Each must be an episode of
    ///   the ordered series; an episode may be in more than one group.
    /// </summary>
    public IReadOnlyList<MetadataGuid> Episodes { get; init; } = [];
}

/// <summary>
///   A user's ordering of a series, kept on this server under the
///   <c>user</c> source. The core gives the ordering and each new group
///   their IDs, and sets the type to <see cref="OrderingType.User"/>.
/// </summary>
public sealed record MetadataLocalOrderingData
{
    /// <summary>
    ///   The series the ordering orders, on any source. An update must name
    ///   the series the ordering was made for.
    /// </summary>
    public required MetadataGuid SeriesID { get; init; }

    /// <summary>
    ///   The ordering's name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///   What the ordering is about, if anything.
    /// </summary>
    public string? Overview { get; init; }

    /// <summary>
    ///   The ordering's groups, in viewing order.
    /// </summary>
    public IReadOnlyList<MetadataLocalOrderingGroupData> Groups { get; init; } = [];
}

/// <summary>
///   One group of a user's ordering, read back as a season of the ordering.
/// </summary>
public sealed record MetadataLocalOrderingGroupData
{
    /// <summary>
    ///   The group to keep, on an update: one of the ordering's own groups,
    ///   which keeps its ID. Left out, the group is new and gets an ID.
    /// </summary>
    public MetadataGuid? ID { get; init; }

    /// <summary>
    ///   The group's name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///   What the group is about, if anything.
    /// </summary>
    public string? Overview { get; init; }

    /// <summary>
    ///   Whether the group holds the ordering's specials. It reads back as
    ///   season <c>0</c> wherever it sits, and the other groups are numbered
    ///   by their place from <c>1</c>. At most one group of an ordering.
    /// </summary>
    public bool IsSpecial { get; init; }

    /// <summary>
    ///   The group's episodes, in viewing order. Each must be an episode of
    ///   the ordered series; an episode may be in more than one group.
    /// </summary>
    public IReadOnlyList<MetadataGuid> Episodes { get; init; } = [];
}

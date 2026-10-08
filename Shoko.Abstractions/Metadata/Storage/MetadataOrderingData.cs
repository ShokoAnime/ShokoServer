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
    ///   The ordering's titles, in order, stored under the ordering's source.
    /// </summary>
    /// <remarks>
    ///   The main title (<see cref="TitleType.Main"/>) is the ordering's
    ///   default. Without one, the ordering gets a synthesized default such as
    ///   <c>TMDB Ordering 5f0c…</c>, never stored.
    /// </remarks>
    public IReadOnlyList<ITitle> Titles { get; init; } = [];

    /// <summary>
    ///   The ordering's overviews, in order, stored like the titles.
    /// </summary>
    public IReadOnlyList<IText> Overviews { get; init; } = [];

    /// <summary>
    ///   What the ordering follows. Neither <see cref="OrderingType.Default"/>
    ///   nor <see cref="OrderingType.User"/>, which the core keeps for the
    ///   default orderings and the users' own.
    /// </summary>
    public OrderingType Type { get; init; } = OrderingType.Unknown;

    /// <summary>
    ///   The networks the ordering follows, in order. Each must be a network
    ///   of the ordering's source already saved through
    ///   <see cref="IMetadataStudioStore.SaveNetworks"/>.
    /// </summary>
    public IReadOnlyList<MetadataGuid> Networks { get; init; } = [];

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
    ///   The group's titles, in order, stored under the ordering's source.
    /// </summary>
    /// <remarks>
    ///   The main title (<see cref="TitleType.Main"/>) is the group's default.
    ///   Without one, the group gets its generic season name, such as
    ///   <c>Season 2</c>, synthesized in the user's languages. Generic names
    ///   for the group's own season number are left out.
    /// </remarks>
    public IReadOnlyList<ITitle> Titles { get; init; } = [];

    /// <summary>
    ///   The group's overviews, in order, stored like the titles.
    /// </summary>
    public IReadOnlyList<IText> Overviews { get; init; } = [];

    /// <summary>
    ///   Whether the group holds the ordering's specials. It reads back as
    ///   season <c>0</c> wherever it sits, and the other groups are numbered
    ///   by their place from <c>1</c>, unless they carry their own
    ///   <see cref="SeasonNumber"/>. At most one group of an ordering.
    /// </summary>
    public bool IsSpecial { get; init; }

    /// <summary>
    ///   The season number the group reads back with, when the source gives
    ///   its groups numbers of their own. At least <c>1</c>, and never set on
    ///   the special group. Left out, the group is numbered by its place.
    /// </summary>
    public int? SeasonNumber { get; init; }

    /// <summary>
    ///   The group's episodes, in viewing order. Each must be an episode of
    ///   the ordered series, and may be in more than one group. A special in
    ///   the special group and a regular one stays a special, numbered in the
    ///   special group; its place in the regular group only says where it airs.
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
    ///   The ordering's titles, in order, stored under the <c>user</c> source.
    /// </summary>
    /// <remarks>
    ///   The main title (<see cref="TitleType.Main"/>) is the ordering's
    ///   default. Without one, the ordering gets a synthesized default such as
    ///   <c>User Ordering 1f0c…</c>, never stored.
    /// </remarks>
    public IReadOnlyList<ITitle> Titles { get; init; } = [];

    /// <summary>
    ///   The ordering's overviews, in order, stored like the titles.
    /// </summary>
    public IReadOnlyList<IText> Overviews { get; init; } = [];

    /// <summary>
    ///   The networks the ordering follows, in order, on any source this
    ///   server knows. A network not stored yet is kept as a stub with an
    ///   empty name until its source saves it. Left out, an update keeps the
    ///   ordering's networks as they are.
    /// </summary>
    public IReadOnlyList<MetadataGuid>? Networks { get; init; }

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
    ///   The group's titles, in order, stored under the <c>user</c> source.
    /// </summary>
    /// <remarks>
    ///   The main title (<see cref="TitleType.Main"/>) is the group's default.
    ///   Without one, the group gets its generic season name, such as
    ///   <c>Season 2</c>, synthesized in the user's languages. Generic names
    ///   for the group's own season number are left out.
    /// </remarks>
    public IReadOnlyList<ITitle> Titles { get; init; } = [];

    /// <summary>
    ///   The group's overviews, in order, stored like the titles.
    /// </summary>
    public IReadOnlyList<IText> Overviews { get; init; } = [];

    /// <summary>
    ///   Whether the group holds the ordering's specials. It reads back as
    ///   season <c>0</c> wherever it sits, and the other groups are numbered
    ///   by their place from <c>1</c>. At most one group of an ordering.
    /// </summary>
    public bool IsSpecial { get; init; }

    /// <summary>
    ///   The group's episodes, in viewing order. Each must be an episode of
    ///   the ordered series, and may be in more than one group. A special in
    ///   the special group and a regular one stays a special, numbered in the
    ///   special group; its place in the regular group only says where it airs.
    /// </summary>
    public IReadOnlyList<MetadataGuid> Episodes { get; init; } = [];
}

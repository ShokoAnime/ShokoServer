using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Storage;

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

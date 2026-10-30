using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Storage;

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

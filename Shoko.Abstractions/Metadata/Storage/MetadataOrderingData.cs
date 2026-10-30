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

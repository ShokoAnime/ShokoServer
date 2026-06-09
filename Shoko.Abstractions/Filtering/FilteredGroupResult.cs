using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Shoko;

namespace Shoko.Abstractions.Filtering;

/// <summary>
///   Result from filtering that includes the group, its hierarchy chain from
///   top-level to the matching sub-group, and all matching series IDs within it.
/// </summary>
public sealed class FilteredGroupResult
{
    /// <summary>
    ///   The resolved group for the filtered result.
    /// </summary>
    public required IShokoGroup Group { get; init; }

    /// <summary>
    ///   All group ID chains for the filtered result.
    /// </summary>
    public required IReadOnlyList<IReadOnlyList<int>> GroupIDChains { get; init; }

    /// <summary>
    ///   All series IDs for the filtered result.
    /// </summary>
    public required IReadOnlySet<int> SeriesIDs { get; init; }
}

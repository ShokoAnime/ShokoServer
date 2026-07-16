using System.Collections.Generic;

namespace Shoko.Server.API.v3.Models.Shoko;

/// <summary>
///   Lightweight version of <see cref="Abstractions.Filtering.FilteredGroupResult"/>
///   that returns the group ID instead of the full <see cref="Group"/> object.
/// </summary>
public sealed class FilteredGroupIDs
{
    /// <summary>
    ///   The group ID for the filtered result.
    /// </summary>
    public required int GroupID { get; init; }

    /// <summary>
    ///   All group ID chains for the filtered result.
    /// </summary>
    public required IReadOnlyList<IReadOnlyList<int>> GroupIDChains { get; init; }

    /// <summary>
    ///   All series IDs for the filtered result.
    /// </summary>
    public required IReadOnlySet<int> SeriesIDs { get; init; }
}

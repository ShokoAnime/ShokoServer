using System;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Metadata.Events;

/// <summary>
///   Dispatched when a series is moved between groups.
/// </summary>
/// <param name="series">The series.</param>
/// <param name="oldGroupID">The id of the group the series was moved from.</param>
/// <param name="newGroupID">The id of the group the series was moved to.</param>
public class SeriesMovedEventArgs(IShokoSeries series, int oldGroupID, int newGroupID) : EventArgs
{
    /// <summary>
    ///   The series info.
    /// </summary>
    public IShokoSeries SeriesInfo { get; init; } = series;

    /// <summary>
    ///   The id of the group the series was moved from.
    /// </summary>
    public int OldGroupID { get; init; } = oldGroupID;

    /// <summary>
    ///   The id of the group the series was moved to.
    /// </summary>
    public int NewGroupID { get; init; } = newGroupID;

    /// <summary>
    ///   The API token of whoever made the change, or <c>null</c> when the
    ///   system did it. Stamped when the event is raised.
    /// </summary>
    public ApiToken? Actor { get; init; }
}

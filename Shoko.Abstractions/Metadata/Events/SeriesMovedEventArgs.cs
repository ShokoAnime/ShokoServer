using System;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Metadata.Events;

/// <summary>
///   Dispatched when a series is moved between groups.
/// </summary>
public class SeriesMovedEventArgs : EventArgs
{
    /// <summary>
    ///   The series info.
    /// </summary>
    public IShokoSeries SeriesInfo { get; init; }

    /// <summary>
    ///   The id of the group the series was moved from.
    /// </summary>
    public int OldGroupID { get; init; }

    /// <summary>
    ///   The id of the group the series was moved to.
    /// </summary>
    public int NewGroupID { get; init; }

    /// <summary>
    ///   Initializes a new instance of the <see cref="SeriesMovedEventArgs"/> class.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <param name="oldGroupID">The id of the group the series was moved from.</param>
    /// <param name="newGroupID">The id of the group the series was moved to.</param>
    public SeriesMovedEventArgs(IShokoSeries series, int oldGroupID, int newGroupID)
    {
        SeriesInfo = series;
        OldGroupID = oldGroupID;
        NewGroupID = newGroupID;
    }

    /// <summary>
    ///   The API token of whoever made the change, or <c>null</c> when the
    ///   system did it. Stamped when the event is raised.
    /// </summary>
    public ApiToken? Actor { get; init; }
}

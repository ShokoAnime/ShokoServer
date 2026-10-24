using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata.Events;

namespace Shoko.Server.API.SignalR.Models;

/// <summary>
/// Dispatched when custom tags are added to or removed from a series.
/// </summary>
/// <param name="args">The event arguments.</param>
public class SeriesCustomTagsChangedSignalRModel(SeriesCustomTagsChangedEventArgs args)
{
    /// <summary>
    /// The Shoko series ID.
    /// </summary>
    public int ShokoSeriesID { get; } = args.Series.LocalID;

    /// <summary>
    /// The custom tags added to the series.
    /// </summary>
    public IReadOnlyList<int> AddedTagIDs { get; } = [.. args.Added.Select(tag => tag.LocalID)];

    /// <summary>
    /// The custom tags removed from the series.
    /// </summary>
    public IReadOnlyList<int> RemovedTagIDs { get; } = [.. args.Removed.Select(tag => tag.LocalID)];
}

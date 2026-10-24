using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Events;

namespace Shoko.Server.API.SignalR.Models;

/// <summary>
/// Dispatched when an ordering of a series is added, updated or removed.
/// </summary>
/// <param name="args">The event arguments.</param>
public class OrderingChangedSignalRModel(OrderingChangedEventArgs args)
{
    /// <summary>
    /// The ordering's full ID.
    /// </summary>
    public string OrderingID { get; } = args.OrderingID.ToString();

    /// <summary>
    /// The full ID of the series the ordering is of.
    /// </summary>
    public string SeriesID { get; } = args.SeriesID.ToString();

    /// <summary>
    /// The Shoko series ID, for an ordering of a Shoko series.
    /// </summary>
    public int? ShokoSeriesID { get; } = ShokoID(args.SeriesID);

    /// <summary>
    /// The local ID of a Shoko entry.
    /// </summary>
    /// <param name="id">The entry's full ID.</param>
    /// <returns>The local ID, or <c>null</c> for an entry of another source.</returns>
    internal static int? ShokoID(MetadataGuid? id)
        => id is { } value && value.Source == MetadataSource.Shoko && value.TryGetNumericID<int>(out var localID) ? localID : null;
}

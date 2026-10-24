using Shoko.Abstractions.Metadata.Events;

namespace Shoko.Server.API.SignalR.Models;

/// <summary>
/// Dispatched when the ordering chosen for a series changes.
/// </summary>
/// <param name="args">The event arguments.</param>
public class PreferredOrderingChangedSignalRModel(PreferredOrderingChangedEventArgs args)
{
    /// <summary>
    /// The series' full ID.
    /// </summary>
    public string SeriesID { get; } = args.SeriesID.ToString();

    /// <summary>
    /// The Shoko series ID, for a Shoko series.
    /// </summary>
    public int? ShokoSeriesID { get; } = OrderingChangedSignalRModel.ShokoID(args.SeriesID);

    /// <summary>
    /// The full ID of the ordering chosen before, or <c>null</c> for the
    /// default ordering.
    /// </summary>
    public string? PreviousOrderingID { get; } = args.PreviousOrderingID?.ToString();

    /// <summary>
    /// The full ID of the ordering chosen now, or <c>null</c> for the default
    /// ordering.
    /// </summary>
    public string? OrderingID { get; } = args.OrderingID?.ToString();
}

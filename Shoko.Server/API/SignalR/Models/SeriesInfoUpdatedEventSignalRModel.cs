using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Enums;

#nullable enable
namespace Shoko.Server.API.SignalR.Models;

public class SeriesInfoUpdatedEventSignalRModel
{
    public SeriesInfoUpdatedEventSignalRModel(SeriesInfoUpdatedEventArgs eventArgs)
    {
        Source = eventArgs.SeriesInfo.Source;
        Reason = eventArgs.Reason;
        SeriesID = eventArgs.SeriesInfo.ID;

        ShokoSeriesIDs = eventArgs.SeriesInfo.ShokoSeriesIDs;
        ShokoGroupIDs = eventArgs.SeriesInfo.ShokoGroupIDs;
    }

    /// <summary>
    /// The provider metadata source.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public DataSourceEnum Source { get; }

    /// <summary>
    /// The update reason.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public UpdateReason Reason { get; }

    /// <summary>
    /// The provided metadata series id.
    /// </summary>
    public int SeriesID { get; }

    /// <summary>
    /// Shoko series ids affected by this update.
    /// </summary>
    public IReadOnlyList<int> ShokoSeriesIDs { get; }

    /// <summary>
    /// Shoko group ids affected by this update.
    /// </summary>
    public IReadOnlyList<int> ShokoGroupIDs { get; }
}

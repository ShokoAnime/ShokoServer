using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Events;

#nullable enable
namespace Shoko.Server.API.SignalR.Models;

public class EpisodeInfoUpdatedEventSignalRModel
{
    public EpisodeInfoUpdatedEventSignalRModel(EpisodeInfoUpdatedEventArgs eventArgs)
    {
        Source = eventArgs.EpisodeInfo.Source;
        Reason = eventArgs.Reason;
        EpisodeID = eventArgs.EpisodeInfo.ID;
        SeriesID = eventArgs.SeriesInfo.ID;
        ShokoEpisodeIDs = eventArgs.EpisodeInfo.ShokoEpisodeIDs;
        ShokoSeriesIDs = eventArgs.SeriesInfo.ShokoSeriesIDs;
    }

    /// <summary>
    /// The provider metadata source.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public DataSource Source { get; }

    /// <summary>
    /// The update reason.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public UpdateReason Reason { get; }

    /// <summary>
    /// The provided metadata episode id.
    /// </summary>
    public int EpisodeID { get; }

    /// <summary>
    /// The provided metadata series id.
    /// </summary>
    public int SeriesID { get; }

    /// <summary>
    /// Shoko episode ids affected by this update.
    /// </summary>
    public IReadOnlyList<int> ShokoEpisodeIDs { get; }

    /// <summary>
    /// Shoko series ids affected by this update.
    /// </summary>
    public IReadOnlyList<int> ShokoSeriesIDs { get; }
}

using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Events;

namespace Shoko.Server.API.SignalR.Models;

public class EpisodeInfoUpdatedEventSignalRModel
{
    public EpisodeInfoUpdatedEventSignalRModel(EpisodeInfoUpdatedEventArgs eventArgs)
    {
        Source = eventArgs.EpisodeInfo.Source;
        Reason = eventArgs.Reason;
        EpisodeID = eventArgs.EpisodeInfo.ID.ID;
        SeriesID = eventArgs.SeriesInfo.ID.ID;
        ShokoEpisodeIDs = eventArgs.EpisodeInfo.ShokoEpisodeIDs;
        ShokoSeriesIDs = eventArgs.SeriesInfo.ShokoSeriesIDs;
    }

    /// <summary>
    /// The provider metadata source.
    /// </summary>
    public MetadataSource Source { get; }

    /// <summary>
    /// The update reason.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public UpdateReason Reason { get; }

    /// <summary>
    /// The provided metadata episode id.
    /// </summary>
    public string EpisodeID { get; }

    /// <summary>
    /// The provided metadata series id.
    /// </summary>
    public string SeriesID { get; }

    /// <summary>
    /// Shoko episode ids affected by this update.
    /// </summary>
    public IReadOnlyList<int> ShokoEpisodeIDs { get; }

    /// <summary>
    /// Shoko series ids affected by this update.
    /// </summary>
    public IReadOnlyList<int> ShokoSeriesIDs { get; }
}

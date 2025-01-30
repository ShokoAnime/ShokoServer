using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Events;

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
        Episodes = eventArgs.Episodes.Select(e => new SeriesInfoUpdatedEventEpisodeDetailsSignalRModel(e)).ToList();
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
    /// The episodes that were added/updated/removed during this event.
    /// </summary>
    public IReadOnlyList<SeriesInfoUpdatedEventEpisodeDetailsSignalRModel> Episodes { get; }

    public class SeriesInfoUpdatedEventEpisodeDetailsSignalRModel
    {
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
        /// Shoko episode ids affected by this update.
        /// </summary>
        public IReadOnlyList<int> ShokoEpisodeIDs { get; }

        public SeriesInfoUpdatedEventEpisodeDetailsSignalRModel(EpisodeInfoUpdatedEventArgs eventArgs)
        {
            Reason = eventArgs.Reason;
            EpisodeID = eventArgs.EpisodeInfo.ID;
            ShokoEpisodeIDs = eventArgs.EpisodeInfo.ShokoEpisodeIDs;
        }
    }
}

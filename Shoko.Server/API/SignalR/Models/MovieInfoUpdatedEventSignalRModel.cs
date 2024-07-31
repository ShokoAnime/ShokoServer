using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Enums;

#nullable enable
namespace Shoko.Server.API.SignalR.Models;

public class MovieInfoUpdatedEventSignalRModel
{
    public MovieInfoUpdatedEventSignalRModel(MovieInfoUpdatedEventArgs eventArgs)
    {
        Source = eventArgs.MovieInfo.Source;
        Reason = eventArgs.Reason;
        MovieID = eventArgs.MovieInfo.ID;
        ShokoEpisodeIDs = eventArgs.MovieInfo.ShokoEpisodeIDs;
        ShokoSeriesIDs = eventArgs.MovieInfo.ShokoSeriesIDs;
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
    /// The provided metadata movie id.
    /// </summary>
    public int MovieID { get; }

    /// <summary>
    /// Shoko episode ids affected by this update.
    /// </summary>
    public IReadOnlyList<int> ShokoEpisodeIDs { get; }

    /// <summary>
    /// Shoko series ids affected by this update.
    /// </summary>
    public IReadOnlyList<int> ShokoSeriesIDs { get; }
}

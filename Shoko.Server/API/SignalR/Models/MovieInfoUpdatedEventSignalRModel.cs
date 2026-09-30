using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Events;

namespace Shoko.Server.API.SignalR.Models;

public class MovieInfoUpdatedEventSignalRModel
{
    public MovieInfoUpdatedEventSignalRModel(MovieInfoUpdatedEventArgs eventArgs)
    {
        Source = eventArgs.MovieInfo.Source;
        Reason = eventArgs.Reason;
        MovieID = eventArgs.MovieInfo.ID.ID;
        ShokoEpisodeIDs = eventArgs.MovieInfo.ShokoEpisodeIDs;
        ShokoSeriesIDs = eventArgs.MovieInfo.ShokoSeriesIDs;
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
    /// The provided metadata movie id.
    /// </summary>
    public string MovieID { get; }

    /// <summary>
    /// Shoko episode ids affected by this update.
    /// </summary>
    public IReadOnlyList<int> ShokoEpisodeIDs { get; }

    /// <summary>
    /// Shoko series ids affected by this update.
    /// </summary>
    public IReadOnlyList<int> ShokoSeriesIDs { get; }
}

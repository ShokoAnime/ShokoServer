using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.API.SignalR.Models;

public class SeriesInfoUpdatedEventSignalRModel
{
    public SeriesInfoUpdatedEventSignalRModel(SeriesInfoUpdatedEventArgs eventArgs)
    {
        Source = eventArgs.SeriesInfo.Source;
        Reason = eventArgs.Reason;
        SeriesID = eventArgs.SeriesInfo.ID;
        // TODO: Add support for more metadata sources when they're hooked up internally.
        switch (Source)
        {
            case DataSourceEnum.AniDB:
                if (eventArgs.SeriesInfo is SVR_AniDB_Anime anidbAnime)
                {
                    var series = RepoFactory.AnimeSeries.GetByAnimeID(eventArgs.SeriesInfo.ID);
                    if (series == null)
                        break;
                    ShokoSeriesIDs = new int[1] { series.AnimeSeriesID };
                    ShokoGroupIDs = series.AllGroupsAbove.Select(g => g.AnimeGroupID).ToArray();
                }
                break;
        }
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
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<int>? ShokoSeriesIDs { get; }

    /// <summary>
    /// Shoko group ids affected by this update.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<int>? ShokoGroupIDs { get; }
}

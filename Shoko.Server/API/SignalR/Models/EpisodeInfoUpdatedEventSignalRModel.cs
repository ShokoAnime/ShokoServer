using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Models;

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
        ShokoEpisodeIDs = [];
        ShokoSeriesIDs = [];
        ShokoGroupIDs = [];
        // TODO: Add support for more metadata sources when they're hooked up internally.
        switch (Source)
        {
            case DataSourceEnum.Shoko when eventArgs.EpisodeInfo is SVR_AnimeEpisode shokoEpisode:
                {
                    ShokoEpisodeIDs = [shokoEpisode.AnimeEpisodeID];
                    ShokoSeriesIDs = [shokoEpisode.AnimeSeriesID];
                    if (eventArgs.SeriesInfo is SVR_AnimeSeries series)
                        ShokoGroupIDs = series.AllGroupsAbove.Select(g => g.AnimeGroupID).ToArray();
                }
                break;
            case DataSourceEnum.AniDB when eventArgs.EpisodeInfo is SVR_AniDB_Episode anidbEpisode && anidbEpisode.AnimeEpisode is SVR_AnimeEpisode shokoEpisode:
                {
                    ShokoEpisodeIDs = [shokoEpisode.AnimeEpisodeID];
                    ShokoSeriesIDs = [shokoEpisode.AnimeSeriesID];
                    if (shokoEpisode.AnimeSeries is SVR_AnimeSeries series)
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

    /// <summary>
    /// Shoko group ids affected by this update.
    /// </summary>
    public IReadOnlyList<int> ShokoGroupIDs { get; }
}

using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Server.Models.Shoko;

namespace Shoko.Server.Actions;

/// <summary>
///   Resolves the videos a scoped MyList action operates on. A MyList entry is
///   per file, so every scope above a video fans out to the files beneath it.
/// </summary>
internal static class MyListActionScope
{
    /// <summary>
    ///   Every file in the group, including its sub-groups. Distinct, because
    ///   one file can be tied to more than one series in the group.
    /// </summary>
    public static IEnumerable<VideoLocal> VideosOf(IShokoGroup group)
        => ((AnimeGroup)group).AllSeries
            .SelectMany(series => series.VideoLocals)
            .DistinctBy(video => video.VideoLocalID);

    /// <summary>
    ///   Every file in the series.
    /// </summary>
    public static IEnumerable<VideoLocal> VideosOf(IShokoSeries series)
        => ((AnimeSeries)series).VideoLocals
            .DistinctBy(video => video.VideoLocalID);

    /// <summary>
    ///   Every file for the episode, which is more than one whenever the
    ///   episode has alternative releases.
    /// </summary>
    public static IEnumerable<VideoLocal> VideosOf(IShokoEpisode episode)
        => ((AnimeEpisode)episode).VideoLocals
            .DistinctBy(video => video.VideoLocalID);
}

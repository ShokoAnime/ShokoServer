using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Video;

namespace Shoko.Server.Actions;

/// <summary>
///   Resolves the videos a scoped Mylist action operates on. A Mylist entry is
///   per file, so every scope above a video fans out to the files beneath it.
/// </summary>
internal static class MylistActionScope
{
    /// <summary>
    ///   Every file in the group, including its sub-groups. Distinct, because
    ///   one file can be tied to more than one series in the group.
    /// </summary>
    public static IEnumerable<IVideo> VideosOf(IShokoGroup group)
        => group.AllSeries
            .SelectMany(series => series.Videos)
            .DistinctBy(video => video.ID);

    /// <summary>
    ///   Every file in the series.
    /// </summary>
    public static IEnumerable<IVideo> VideosOf(IShokoSeries series)
        => series.Videos
            .DistinctBy(video => video.ID);

    /// <summary>
    ///   Every file for the episode, which is more than one whenever the
    ///   episode has alternative releases.
    /// </summary>
    public static IEnumerable<IVideo> VideosOf(IShokoEpisode episode)
        => episode.Videos
            .DistinctBy(video => video.ID);
}

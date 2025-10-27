using System;

namespace Shoko.Models.Server;

public class AnimeSeries_User
{
    /// <summary>
    /// Local DB Row ID.
    /// </summary>
    public int AnimeSeries_UserID { get; set; }

    /// <summary>
    /// Shoko User ID.
    /// </summary>
    public int JMMUserID { get; set; }

    /// <summary>
    /// Shoko Series ID.
    /// </summary>
    public int AnimeSeriesID { get; set; }

    /// <summary>
    /// The number of episodes that have not been watched to completion,
    /// and are not hidden.
    /// </summary>
    public int UnwatchedEpisodeCount { get; set; }

    /// <summary>
    /// The number of episodes that have not been watched to completion,
    /// and are hidden.
    /// </summary>
    public int HiddenUnwatchedEpisodeCount { get; set; }

    /// <summary>
    /// The number of episodes that have been watched to completion.
    /// </summary>
    public int WatchedEpisodeCount { get; set; }

    /// <summary>
    /// The last time the series was watched to completion.
    /// </summary>
    public DateTime? WatchedDate { get; set; }

    /// <summary>
    ///  How many times videos have been started/played for the series. Only used by Shoko Desktop and APIv1.
    /// </summary>
    public int PlayedCount { get; set; }

    /// <summary>
    /// How many videos have been played to completion for the series.
    /// </summary>
    public int WatchedCount { get; set; }

    /// <summary>
    ///  How many times videos have been stopped for the series. Only used by Shoko Desktop and APIv1.
    /// </summary>
    public int StoppedCount { get; set; }

    /// <summary>
    /// The last time an episode was updated, regardless of if it was
    /// watched to completion or not. Used to determine continue watching
    /// and next-up order for the series and on the dashboard.
    /// </summary>
    public DateTime? LastEpisodeUpdate { get; set; }
}

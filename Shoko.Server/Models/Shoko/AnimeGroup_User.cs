using System;

#nullable enable
namespace Shoko.Server.Models.Shoko;

public class AnimeGroup_User
{
    #region DB Columns

    public int AnimeGroup_UserID { get; set; }

    public int JMMUserID { get; set; }

    public int AnimeGroupID { get; set; }

    public int UnwatchedEpisodeCount { get; set; }

    public int WatchedEpisodeCount { get; set; }

    public DateTime? WatchedDate { get; set; }

    public int PlayedCount { get; set; }

    public int WatchedCount { get; set; }

    public int StoppedCount { get; set; }

    #endregion
}

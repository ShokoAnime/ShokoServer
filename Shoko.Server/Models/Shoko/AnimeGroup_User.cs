using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.User;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models.Shoko;

public class AnimeGroup_User : IGroupUserData
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

    /// <summary>
    ///   The unique tags assigned to the group by the user.
    /// </summary>
    public List<string> UserTags { get; set; } = [];

    /// <summary>
    ///   The last time the group user data was updated.
    /// </summary>
    public DateTime LastUpdated { get; set; }

    #endregion

    /// <summary>
    ///   The Shoko Group, if available.
    /// </summary>
    public AnimeGroup? AnimeGroup => RepoFactory.AnimeGroup.GetByID(AnimeGroupID);

    #region IUserData Implementation

    int IUserData.UserID => JMMUserID;

    DateTime IUserData.LastUpdatedAt => LastUpdated;

    IUser IUserData.User => RepoFactory.JMMUser.GetByID(JMMUserID) ??
        throw new NullReferenceException($"Unable to find IUser with the given id. (User={JMMUserID})");

    #endregion

    #region IGroupUserData Implementation

    int IGroupUserData.GroupID => AnimeGroupID;

    DateTime? IGroupUserData.LastPlayedAt => WatchedDate;

    int IGroupUserData.PlaybackCount => WatchedCount;

    DateTime? IGroupUserData.LastSeriesUpdatedAt
        => (AnimeGroup?.AllSeries ?? [])
            .Select(ser => RepoFactory.AnimeSeries_User.GetByUserAndSeriesID(JMMUserID, ser.AnimeSeriesID)?.LastUpdated)
            .WhereNotNull()
            .OrderDescending()
            .FirstOrDefault();

    DateTime? IGroupUserData.LastEpisodeUpdatedAt
        => (AnimeGroup?.AllSeries ?? [])
            .Select(ser => RepoFactory.AnimeSeries_User.GetByUserAndSeriesID(JMMUserID, ser.AnimeSeriesID)?.LastEpisodeUpdate)
            .WhereNotNull()
            .OrderDescending()
            .FirstOrDefault();

    DateTime? IGroupUserData.LastVideoUpdatedAt
        => (AnimeGroup?.AllSeries ?? [])
            .Select(ser => RepoFactory.AnimeSeries_User.GetByUserAndSeriesID(JMMUserID, ser.AnimeSeriesID)?.LastVideoUpdate)
            .WhereNotNull()
            .OrderDescending()
            .FirstOrDefault();

    IReadOnlyList<string> IGroupUserData.UserTags => UserTags;

    IShokoGroup? IGroupUserData.Group => AnimeGroup;

    #endregion
}

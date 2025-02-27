using System;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models;

public class SVR_VideoLocal_User : VideoLocal_User, IVideoUserData
{
    public SVR_VideoLocal_User() { }

    public SVR_VideoLocal_User(int userID, int fileID)
    {
        JMMUserID = userID;
        VideoLocalID = fileID;
        LastUpdated = DateTime.Now;
    }

    /// <summary>
    /// Where to resume the playback of the <see cref="Models.VideoLocal"/>
    ///  as a <see cref="TimeSpan"/>.
    /// </summary>
    public TimeSpan? ResumePositionTimeSpan
    {
        get => ResumePosition > 0 ? TimeSpan.FromMilliseconds(ResumePosition) : null;
        set => ResumePosition = value.HasValue ? (long)Math.Round(value.Value.TotalMilliseconds) : 0;
    }

    public SVR_JMMUser User => RepoFactory.JMMUser.GetByID(JMMUserID);

    /// <summary>
    /// Get the related <see cref="Models.VideoLocal"/>.
    /// </summary>
    public VideoLocal? VideoLocal
        => RepoFactory.VideoLocal.GetByID(VideoLocalID);

    public override string ToString()
    {
        var video = VideoLocal;
        if (video == null)
            return $"{VideoLocalID} -- User {JMMUserID}";

#pragma warning disable CS0618
        return $"{video.FileName} --- {video.Hash} --- User {JMMUserID}";
#pragma warning restore CS0618
    }

    #region IUserData Implementation

    int IUserData.UserID => JMMUserID;

    DateTime IUserData.LastUpdatedAt => LastUpdated;

    IShokoUser IUserData.User => User ??
        throw new NullReferenceException($"Unable to find IShokoUser with the given id. (User={JMMUserID})");

    #endregion

    #region IVideoUserData Implementation

    int IVideoUserData.VideoID => VideoLocalID;

    int IVideoUserData.PlaybackCount => WatchedCount;

    TimeSpan IVideoUserData.ResumePosition => ResumePositionTimeSpan ?? TimeSpan.Zero;

    DateTime? IVideoUserData.LastPlayedAt => WatchedDate;

    IVideo IVideoUserData.Video => VideoLocal ??
        throw new NullReferenceException($"Unable to find IVideo with the given id. (Video={VideoLocalID})");

    #endregion
}

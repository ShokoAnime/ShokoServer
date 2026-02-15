using System;
using Shoko.Abstractions.User;
using Shoko.Abstractions.UserData;
using Shoko.Abstractions.Video;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.Shoko;

public class VideoLocal_User : IVideoUserData
{
    public int VideoLocal_UserID { get; set; }

    public int JMMUserID { get; set; }

    public int VideoLocalID { get; set; }

    public DateTime? WatchedDate { get; set; }

    public long ResumePosition { get; set; }

    public DateTime LastUpdated { get; set; }

    public int WatchedCount { get; set; }

    /// <summary>
    /// Where to resume the playback of the <see cref="Shoko.VideoLocal"/>
    ///  as a <see cref="TimeSpan"/>.
    /// </summary>
    public TimeSpan? ProgressPosition
    {
        get => ResumePosition > 0 ? TimeSpan.FromMilliseconds(ResumePosition) : null;
        set => ResumePosition = value.HasValue ? (long)Math.Round(value.Value.TotalMilliseconds) : 0;
    }

    public JMMUser User
        => RepoFactory.JMMUser.GetByID(JMMUserID);

    /// <summary>
    /// Get the related <see cref="Shoko.VideoLocal"/>.
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

    IUser IUserData.User => User ??
        throw new NullReferenceException($"Unable to find IUser with the given id. (User={JMMUserID})");

    #endregion

    #region IVideoUserData Implementation

    int IVideoUserData.VideoID => VideoLocalID;

    int IVideoUserData.PlaybackCount => WatchedCount;

    TimeSpan IVideoUserData.ProgressPosition => ProgressPosition ?? TimeSpan.Zero;

    DateTime? IVideoUserData.LastPlayedAt => WatchedDate;

    IVideo? IVideoUserData.Video => VideoLocal;

    #endregion
}

using System;
using Shoko.Models.Server;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models;

public class SVR_VideoLocal_User : VideoLocal_User
{
    public SVR_VideoLocal_User() { }

    public SVR_VideoLocal_User(int userID, int fileID)
    {
        JMMUserID = userID;
        VideoLocalID = fileID;
        LastUpdated = DateTime.Now;
    }

    /// <summary>
    /// Where to resume the playback of the <see cref="SVR_VideoLocal"/>
    ///  as a <see cref="TimeSpan"/>.
    /// </summary>
    public TimeSpan? ResumePositionTimeSpan
    {
        get => ResumePosition > 0 ? TimeSpan.FromMilliseconds(ResumePosition) : null;
        set => ResumePosition = value.HasValue ? (long)Math.Round(value.Value.TotalMilliseconds) : 0;
    }

    /// <summary>
    /// Get the related <see cref="SVR_VideoLocal"/>.
    /// </summary>
    public SVR_VideoLocal GetVideoLocal()
    {
        return RepoFactory.VideoLocal.GetByID(VideoLocalID);
    }

    public override string ToString()
    {
        var file = GetVideoLocal();
#pragma warning disable CS0618
        return $"{file.FileName} --- {file.Hash} --- User {JMMUserID}";
#pragma warning restore CS0618
    }
}

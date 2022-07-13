using System;
using Shoko.Models.Server;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models
{
    public class SVR_VideoLocal_User : VideoLocal_User
    {
        /// <summary>
        /// Where to resume the playback of the <see cref="SVR_VideoLocal"/>
        ///  as a <see cref="TimeSpan"/>.
        /// </summary>
        public TimeSpan? ResumePositionTimeSpan
            => ResumePosition > 0 ?  new TimeSpan(0, 0, 0, 0, (int)ResumePosition) : null;

        /// <summary>
        /// Get the related <see cref="SVR_VideoLocal"/>.
        /// </summary>
        public SVR_VideoLocal GetVideoLocal()
            => RepoFactory.VideoLocal.GetByID(VideoLocalID);

        /// <summary>
        /// Get the related <see cref="SVR_JMMUser"/>.
        /// </summary>
        public SVR_JMMUser GetUser()
            => RepoFactory.JMMUser.GetByID(JMMUserID);

        public override string ToString()
        {
            var file = GetVideoLocal();
            return $"{file.FileName} --- {file.Hash} --- User {JMMUserID}";
        }
    }
}

using System;

namespace Shoko.Models.Server
{
    public class VideoLocal_User
    {
        public VideoLocal_User()
        {
        }
        public int VideoLocal_UserID { get; private set; }
        public int JMMUserID { get; set; }
        public int VideoLocalID { get; set; }
        public DateTime? WatchedDate { get; set; }
        public long ResumePosition { get; set; }
    }
}
using System;
using NLog;

namespace JMMServer.Entities
{
    public class VideoLocal_User
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public int VideoLocal_UserID { get; private set; }
        public int JMMUserID { get; set; }
        public int VideoLocalID { get; set; }
        public DateTime WatchedDate { get; set; }
    }
}
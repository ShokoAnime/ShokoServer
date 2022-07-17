using System;

namespace Shoko.Server.Providers.AniDB.UDP.User
{
    public class ResponseMyListFile
    {
        public bool IsWatched { get; set; }
        public DateTime? WatchedDate { get; set; }
        public MyList_State? State { get; set; }
        public int MyListID { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}

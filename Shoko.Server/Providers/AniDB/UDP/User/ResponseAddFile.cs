using System;

namespace Shoko.Server.Providers.AniDB.UDP.User
{
    public class ResponseAddFile
    {
        public bool IsWatched { get; set; }
        public DateTime? WatchedDate { get; set; }
        public GetFile_State? State { get; set; }
        public int MyListID { get; set; }
    }
}

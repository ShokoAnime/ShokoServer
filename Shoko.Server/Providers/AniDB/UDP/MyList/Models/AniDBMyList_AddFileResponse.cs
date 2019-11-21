using System;
using Shoko.Models.Enums;

namespace Shoko.Server.Providers.AniDB.MyList
{
    public class AniDBMyList_AddFileResponse
    {
        public bool IsWatched { get; set; }
        public DateTime? WatchedDate { get; set; }
        public AniDBFile_State? State { get; set; }
        public int MyListID { get; set; }
    }
}
using System.Collections.Generic;

namespace Shoko.Models
{
    public class Contract_Trakt_Activity
    {
        public bool HasTraktAccount { get; set; }
        public List<Contract_Trakt_Friend> TraktFriends { get; set; }
    }
}
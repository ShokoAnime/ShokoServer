using System;


namespace Shoko.Models.Server
{
    public class Trakt_Friend
    {
        public Trakt_Friend()
        {
        }
        public int Trakt_FriendID { get; private set; }
        public string Username { get; set; }
        public string FullName { get; set; }
        public string Gender { get; set; }
        public string Age { get; set; }
        public string Location { get; set; }
        public string About { get; set; }
        public int Joined { get; set; }
        public string Avatar { get; set; }
        public string Url { get; set; }
        public DateTime LastAvatarUpdate { get; set; }
    }
}
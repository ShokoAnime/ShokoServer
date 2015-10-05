using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMMModels
{
    public class Trakt_Friend
    {
        public string Id { get; set; }
        public string UserName { get; set; }
        public string FullName { get; set; }
        public string FullImagePath { get; set; }
        public string Gender { get; set; }
        public string Age { get; set; }
        public string Location { get; set; }
        public string About { get; set; }
        public bool Joined { get; set; }
        public string Avatar { get; set; }
        public string Url { get; set; }
        public DateTime LastAvatarUpdate { get; set; }
    }
}

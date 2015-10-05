using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JMMModels.Childs;

namespace JMMModels
{
    public class JMMUser
    {
        public string Id { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public bool IsPasswordRequired { get; set; }
        public bool IsAdmin { get; set; }
        public bool CanEditServerSettings { get; set; }
        public bool IsMasterAccount { get; set; } //Master ANIDB ACCOUNT for anime  (only one)

        public string ParentId { get; set; } // If present use Parent Authentication
        public bool IsRealUserAccount { get; set; } // If true, matain own set of votes, watched state, etc.

        public List<object> Authorizations { get; set; } 
        public List<string> RestrictedTagsIds { get; set; }
        public List<string> RestrictedCustomTagsIds { get; set; }

    }
}

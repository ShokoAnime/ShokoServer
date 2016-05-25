using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace JMMServer.Providers.TraktTV.Contracts
{
    [DataContract]
    public class TraktV2Follower
    {
        [DataMember(Name = "followed_at")]
        public string followed_at { get; set; }

        [DataMember(Name = "user")]
        public TraktV2User user { get; set; }
    }
}

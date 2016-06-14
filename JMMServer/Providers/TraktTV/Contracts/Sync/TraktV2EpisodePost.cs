using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace JMMServer.Providers.TraktTV.Contracts
{
    [DataContract]
    public class TraktV2EpisodePost
    {
        [DataMember(Name = "ids")]
        public TraktV2EpisodeIds ids { get; set; }

    }
}

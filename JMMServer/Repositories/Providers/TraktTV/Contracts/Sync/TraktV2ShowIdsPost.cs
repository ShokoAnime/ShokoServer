using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace JMMServer.Providers.TraktTV.Contracts
{
    [DataContract]
    public class TraktV2ShowIdsPost
    {
        [DataMember(Name = "slug")]
        public string slug { get; set; }
    }
}

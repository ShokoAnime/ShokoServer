using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace JMMServer.Providers.TraktTV.Contracts
{
    [DataContract(Name = "fanart")]
    public class TraktV2Fanart
    {
        [DataMember(Name = "full")]
        public string full { get; set; }

        [DataMember(Name = "medium")]
        public string medium { get; set; }

        [DataMember(Name = "thumb")]
        public string thumb { get; set; }
    }
}

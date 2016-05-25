using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace JMMServer.Providers.TraktTV.Contracts
{
    [DataContract(Name = "airs")]
    public class TraktV2Airs
    {
        [DataMember(Name = "day")]
        public string day { get; set; }

        [DataMember(Name = "time")]
        public string time { get; set; }

        [DataMember(Name = "timezone")]
        public string timezone { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace JMMServer.Providers.TraktTV.Contracts
{
    [DataContract(Name = "show")]
    public class TraktV2ShowPost
    {
        [DataMember(Name = "ids")]
        public TraktV2ShowIdsPost ids { get; set; }
    }
}

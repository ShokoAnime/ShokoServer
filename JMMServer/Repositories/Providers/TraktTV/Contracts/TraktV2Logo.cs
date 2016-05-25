using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace JMMServer.Providers.TraktTV.Contracts
{
    [DataContract(Name = "logo")]
    public class TraktV2Logo
    {
        public string full { get; set; }
    }
}

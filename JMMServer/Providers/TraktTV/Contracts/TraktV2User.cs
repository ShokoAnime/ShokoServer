using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace JMMServer.Providers.TraktTV.Contracts
{
    [DataContract(Name = "user")]
    public class TraktV2User
    {
        [DataMember(Name = "username")]
        public string username { get; set; }

        [DataMember(Name = "_private")]
        public bool _private { get; set; }

        [DataMember(Name = "name")]
        public string name { get; set; }

        [DataMember(Name = "vip")]
        public bool vip { get; set; }

        [DataMember(Name = "vip_ep")]
        public bool vip_ep { get; set; }
    }
}

using System.Runtime.Serialization;

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
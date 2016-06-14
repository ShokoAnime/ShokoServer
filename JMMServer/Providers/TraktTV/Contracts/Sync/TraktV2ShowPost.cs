using System.Runtime.Serialization;

namespace JMMServer.Providers.TraktTV.Contracts
{
    [DataContract(Name = "show")]
    public class TraktV2ShowPost
    {
        [DataMember(Name = "ids")]
        public TraktV2ShowIdsPost ids { get; set; }
    }
}
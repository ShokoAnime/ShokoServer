using System.Runtime.Serialization;

namespace JMMServer.Providers.TraktTV.Contracts
{
    [DataContract]
    public class TraktV2ShowIdsPost
    {
        [DataMember(Name = "slug")]
        public string slug { get; set; }
    }
}
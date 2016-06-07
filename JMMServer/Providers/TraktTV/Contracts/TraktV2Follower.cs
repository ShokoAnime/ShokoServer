using System.Runtime.Serialization;

namespace JMMServer.Providers.TraktTV.Contracts
{
    [DataContract]
    public class TraktV2Follower
    {
        [DataMember(Name = "followed_at")]
        public string followed_at { get; set; }

        [DataMember(Name = "user")]
        public TraktV2User user { get; set; }
    }
}
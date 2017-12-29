using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts
{
    [DataContract]
    public class TraktV2EpisodePost
    {
        [DataMember(Name = "ids")]
        public TraktV2EpisodeIds ids { get; set; }
    }
}
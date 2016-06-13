using System.Collections.Generic;
using System.Runtime.Serialization;

namespace JMMServer.Providers.TraktTV.Contracts
{
    [DataContract]
    public class TraktV2SyncCollectionEpisodes
    {
        [DataMember(Name = "episodes")]
        public List<TraktV2EpisodePost> episodes { get; set; }
    }
}
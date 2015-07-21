using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace JMMServer.Providers.TraktTV.Contracts
{
    [DataContract]
    public class TraktV2EpisodeIds
    {
        [DataMember(Name = "trakt")]
        public int trakt { get; set; }

        [DataMember(Name = "tvdb")]
        public int? tvdb { get; set; }

        [DataMember(Name = "imdb")]
        public int? imdb { get; set; }

        [DataMember(Name = "tmdb")]
        public int? tmdb { get; set; }

        [DataMember(Name = "tvrage")]
        public int? tvrage { get; set; }
    }
}

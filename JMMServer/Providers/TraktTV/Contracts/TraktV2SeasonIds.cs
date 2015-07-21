using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace JMMServer.Providers.TraktTV.Contracts
{
    [DataContract(Name = "ids")]
    public class TraktV2SeasonIds
    {
        [DataMember(Name = "trakt")]
        public int trakt { get; set; }

        [DataMember(Name = "tvdb")]
        public string tvdb { get; set; }

        [DataMember(Name = "tmdb")]
        public string tmdb { get; set; }

        [DataMember(Name = "tvrage")]
        public string tvrage { get; set; }
    }
}

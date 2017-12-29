using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts
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
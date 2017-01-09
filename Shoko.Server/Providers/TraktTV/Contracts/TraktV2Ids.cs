using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts
{
    [DataContract(Name = "ids")]
    public class TraktV2Ids
    {
        [DataMember(Name = "trakt")]
        public int trakt { get; set; }

        [DataMember(Name = "slug")]
        public string slug { get; set; }

        [DataMember(Name = "tvdb")]
        public int? tvdb { get; set; }

        [DataMember(Name = "imdb")]
        public string imdb { get; set; }

        [DataMember(Name = "tmdb")]
        public int? tmdb { get; set; }

        [DataMember(Name = "tvrage")]
        public int? tvrage { get; set; }
    }
}
using System.Runtime.Serialization;

namespace JMMServer.Providers.TraktTV.Contracts
{
    [DataContract(Name = "images")]
    public class TraktV2Images
    {
        [DataMember(Name = "poster")]
        public TraktV2Poster poster { get; set; }

        [DataMember(Name = "fanart")]
        public TraktV2Fanart fanart { get; set; }
    }
}
using System.Runtime.Serialization;

namespace JMMServer.Providers.TraktTV.Contracts
{
    [DataContract]
    public class TraktV2EpisodeImage
    {
        [DataMember(Name = "screenshot")]
        public TraktV2EpisodeScreenshot screenshot { get; set; }
    }
}
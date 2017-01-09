using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts
{
    [DataContract(Name = "screenshot")]
    public class TraktV2EpisodeScreenshot
    {
        [DataMember(Name = "full")]
        public string full { get; set; }

        [DataMember(Name = "medium")]
        public string medium { get; set; }

        [DataMember(Name = "thumb")]
        public string thumb { get; set; }
    }
}
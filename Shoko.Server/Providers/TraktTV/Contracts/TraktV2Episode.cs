using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts
{
    [DataContract]
    public class TraktV2Episode
    {
        [DataMember(Name = "season")]
        public int season { get; set; }

        [DataMember(Name = "number")]
        public int number { get; set; }

        [DataMember(Name = "title")]
        public string title { get; set; }

        [DataMember(Name = "ids")]
        public TraktV2EpisodeIds ids { get; set; }
    }
}
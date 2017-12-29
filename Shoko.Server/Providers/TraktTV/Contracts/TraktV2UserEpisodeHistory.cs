using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts
{
    [DataContract]
    public class TraktV2UserEpisodeHistory
    {
        [DataMember(Name = "watched_at")]
        public string watched_at { get; set; }

        [DataMember(Name = "action")]
        public string action { get; set; } // scrobble / checkin / watch

        [DataMember(Name = "episode")]
        public TraktV2Episode episode { get; set; }

        [DataMember(Name = "show")]
        public TraktV2Show show { get; set; }
    }
}
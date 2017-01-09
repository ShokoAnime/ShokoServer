using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts
{
    [DataContract]
    public class TraktV2EpisodeIds
    {
        [DataMember(Name = "trakt")]
        public string trakt { get; set; }

        [DataMember(Name = "tvdb")]
        public string tvdb { get; set; }

        [DataMember(Name = "imdb")]
        public string imdb { get; set; }

        [DataMember(Name = "tmdb")]
        public string tmdb { get; set; }

        [DataMember(Name = "slug")]
        public string slug { get; set; }

        [DataMember(Name = "tvrage")]
        public string tvrage { get; set; }

        public int? TraktID
        {
            get
            {
                int traktID = 0;
                if (int.TryParse(trakt, out traktID))
                    return traktID;
                else
                    return null;
            }
        }
    }
}
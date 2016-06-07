using System.Runtime.Serialization;

namespace JMMServer.Providers.TraktTV.Contracts
{
    [DataContract]
    internal class TraktV2ScrobbleMovie
    {
        [DataMember(Name = "movie")]
        public TraktV2Movie movie { get; set; }

        [DataMember(Name = "progress")]
        public float progress { get; set; }

        public void Init(float progressVal, string traktSlug, string traktId)
        {
            progress = progressVal;
            movie = new TraktV2Movie();
            movie.ids = new TraktV2Ids();
            movie.ids.slug = traktSlug;
            var traktID = 0;
            int.TryParse(traktId, out traktID);
            movie.ids.trakt = traktID;
        }
    }
}
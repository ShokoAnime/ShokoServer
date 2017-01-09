using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts
{
    [DataContract]
    class TraktV2ScrobbleMovie
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
            int traktID = 0;
            int.TryParse(traktId, out traktID);
            movie.ids.trakt = traktID;
        }
    }
}
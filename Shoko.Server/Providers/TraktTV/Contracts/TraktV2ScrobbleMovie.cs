using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts;

[DataContract]
internal class TraktV2ScrobbleMovie
{
    [DataMember(Name = "movie")] public TraktV2Movie movie { get; set; }

    [DataMember(Name = "progress")] public float progress { get; set; }

    public void Init(float progressVal, string traktSlug, string traktId)
    {
        progress = progressVal;
        movie = new TraktV2Movie { ids = new TraktV2Ids { slug = traktSlug } };
        int.TryParse(traktId, out var traktID);
        movie.ids.trakt = traktID;
    }
}

using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts;

[DataContract]
internal class TraktV2ScrobbleMovie
{
    [DataMember(Name = "movie")]
    public TraktV2Movie Movie { get; set; }

    [DataMember(Name = "progress")]
    public float Progress { get; set; }

    public void Init(float progressVal, string traktSlug, string traktId)
    {
        Progress = progressVal;
        Movie = new TraktV2Movie { IDs = new TraktV2Ids { TraktSlug = traktSlug } };
        int.TryParse(traktId, out var traktID);
        Movie.IDs.TraktID = traktID;
    }
}

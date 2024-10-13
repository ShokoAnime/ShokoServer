using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts;

[DataContract(Name = "movie")]
public class TraktV2Movie
{
    [DataMember(Name = "title")]
    public string Title { get; set; }

    [DataMember(Name = "overview")]
    public string Overview { get; set; }

    [DataMember(Name = "year")]
    public int? Year { get; set; }

    [DataMember(Name = "ids")]
    public TraktV2Ids IDs { get; set; }

    public string MovieURL => string.Format(TraktURIs.WebsiteMovie, IDs.TraktSlug);

    public override string ToString()
    {
        return string.Format("TraktV2Movie: {0}", Title);
    }
}

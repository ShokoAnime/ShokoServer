using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts;

[DataContract(Name = "show")]
public class TraktV2Show
{
    [DataMember(Name = "title")]
    public string Title { get; set; }

    [DataMember(Name = "overview")]
    public string Overview { get; set; }

    [DataMember(Name = "year")]
    public int? Year { get; set; }

    [DataMember(Name = "ids")]
    public TraktV2Ids IDs { get; set; }

    public string ShowURL => string.Format(TraktURIs.WebsiteShow, IDs.TraktSlug);

    public override string ToString()
    {
        return string.Format("TraktV2Show: {0}", Title);
    }
}

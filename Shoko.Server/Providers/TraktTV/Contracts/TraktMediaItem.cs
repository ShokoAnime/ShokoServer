using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts;

[DataContract]
public class TraktMediaItem
{
    [DataMember(Name = "title")]
    public string Title { get; set; }

    [DataMember(Name = "year")]
    public int? Year { get; set; }

    [DataMember(Name = "ids")]
    public TraktIds IDs { get; set; }
}

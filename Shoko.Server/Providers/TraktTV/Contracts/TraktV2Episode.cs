using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts;

[DataContract]
public class TraktV2Episode
{
    [DataMember(Name = "season")]
    public int SeasonNumber { get; set; }

    [DataMember(Name = "number")]
    public int EpisodeNumber { get; set; }

    [DataMember(Name = "title")]
    public string Title { get; set; }

    [DataMember(Name = "ids")]
    public TraktV2EpisodeIds IDs { get; set; }
}

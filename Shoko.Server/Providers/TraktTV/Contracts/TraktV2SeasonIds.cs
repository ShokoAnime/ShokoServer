using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts;

[DataContract(Name = "ids")]
public class TraktV2SeasonIds
{
    [DataMember(Name = "trakt")]
    public int TraktID { get; set; }

    [DataMember(Name = "tvdb")]
    public string TvdbID { get; set; }

    [DataMember(Name = "tmdb")]
    public string TmdbID { get; set; }

    [DataMember(Name = "tvrage")]
    public string TvRageID { get; set; }
}

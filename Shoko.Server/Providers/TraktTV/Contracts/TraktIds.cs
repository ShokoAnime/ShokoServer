using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts;

[DataContract]
public class TraktIds
{
    [DataMember(Name = "trakt")]
    public int TraktID { get; set; }

    [DataMember(Name = "slug")]
    public string TraktSlug { get; set; }

    [DataMember(Name = "tvdb")]
    public int? TvdbID { get; set; }

    [DataMember(Name = "imdb")]
    public string ImdbID { get; set; }

    [DataMember(Name = "tmdb")]
    public int? TmdbID { get; set; }

    [DataMember(Name = "tvrage")]
    public int? TvRageID { get; set; }
}

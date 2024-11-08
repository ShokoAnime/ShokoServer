using System.Runtime.Serialization;
using Shoko.Models.Client;

namespace Shoko.Server.Providers.TraktTV.Contracts;

[DataContract]
public class TraktV2SearchShowResult
{
    [DataMember(Name = "type")]
    public string Type { get; set; }

    [DataMember(Name = "score")]
    public string Score { get; set; }

    [DataMember(Name = "show")]
    public TraktV2Show Show { get; set; }

    public override string ToString()
        => string.Format("{0} - {1} - {2}", Show.Title, Show.Year, Show.Overview);

    public CL_TraktTVShowResponse ToContract()
        => new()
        {
            title = Show.Title,
            year = Show.Year.ToString(),
            url = string.Format(TraktURIs.WebsiteShow, Show.IDs.TraktSlug),
            first_aired = string.Empty,
            country = string.Empty,
            overview = Show.Overview,
            tvdb_id = Show.IDs.TvdbID.ToString(),
        };
}

using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts;

[DataContract(Name = "show")]
public class TraktV2ShowExtended
{
    [DataMember(Name = "title")]
    public string Title { get; set; }

    [DataMember(Name = "year")]
    public int Year { get; set; }

    [DataMember(Name = "ids")]
    public TraktV2Ids IDs { get; set; }

    [DataMember(Name = "overview")]
    public string Overview { get; set; }

    [DataMember(Name = "first_aired")]
    public string FirstAired { get; set; }

    [DataMember(Name = "airs")]
    public TraktV2Airs Airs { get; set; }

    [DataMember(Name = "runtime")]
    public string Runtime { get; set; }

    [DataMember(Name = "certification")]
    public string Certification { get; set; }

    [DataMember(Name = "network")]
    public string Network { get; set; }

    [DataMember(Name = "country")]
    public string Country { get; set; }

    [DataMember(Name = "trailer")]
    public string Trailer { get; set; }

    [DataMember(Name = "homepage")]
    public string Homepage { get; set; }

    [DataMember(Name = "status")]
    public string Status { get; set; }

    [DataMember(Name = "rating")]
    public float Rating { get; set; }

    [DataMember(Name = "votes")]
    public int Votes { get; set; }

    [DataMember(Name = "updated_at")]
    public string UpdatedAt { get; set; }

    [DataMember(Name = "language")]
    public string Language { get; set; }

    [DataMember(Name = "available_translations")]
    public string[] AvailableTranslations { get; set; }

    [DataMember(Name = "genres")]
    public string[] Genres { get; set; }

    [DataMember(Name = "aired_episodes")]
    public int AiredEpisodes { get; set; }

    public override string ToString()
    {
        return string.Format("{0} ({1})", Title, Year);
    }

    public string URL => string.Format(TraktURIs.WebsiteShow, IDs.TraktSlug);
}

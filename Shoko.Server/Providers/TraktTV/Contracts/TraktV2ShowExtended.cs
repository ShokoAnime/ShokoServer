using System.Runtime.Serialization;

namespace Shoko.Server.Providers.TraktTV.Contracts
{
    [DataContract(Name = "show")]
    public class TraktV2ShowExtended
    {
        [DataMember(Name = "title")]
        public string title { get; set; }

        [DataMember(Name = "year")]
        public int year { get; set; }

        [DataMember(Name = "ids")]
        public TraktV2Ids ids { get; set; }

        [DataMember(Name = "overview")]
        public string overview { get; set; }

        [DataMember(Name = "first_aired")]
        public string first_aired { get; set; }

        [DataMember(Name = "airs")]
        public TraktV2Airs airs { get; set; }

        [DataMember(Name = "runtime")]
        public string runtime { get; set; }

        [DataMember(Name = "certification")]
        public string certification { get; set; }

        [DataMember(Name = "network")]
        public string network { get; set; }

        [DataMember(Name = "country")]
        public string country { get; set; }

        [DataMember(Name = "trailer")]
        public string trailer { get; set; }

        [DataMember(Name = "homepage")]
        public string homepage { get; set; }

        [DataMember(Name = "status")]
        public string status { get; set; }

        [DataMember(Name = "rating")]
        public float rating { get; set; }

        [DataMember(Name = "votes")]
        public int votes { get; set; }

        [DataMember(Name = "updated_at")]
        public string updated_at { get; set; }

        [DataMember(Name = "language")]
        public string language { get; set; }

        [DataMember(Name = "available_translations")]
        public string[] available_translations { get; set; }

        [DataMember(Name = "genres")]
        public string[] genres { get; set; }

        [DataMember(Name = "aired_episodes")]
        public int aired_episodes { get; set; }

        public override string ToString()
        {
            return string.Format("{0} ({1})", title, year);
        }

        public string ShowURL
        {
            get { return string.Format(TraktURIs.WebsiteShow, ids.slug); }
        }
    }
}
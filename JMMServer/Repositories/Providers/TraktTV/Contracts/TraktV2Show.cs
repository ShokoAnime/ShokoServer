using System.Runtime.Serialization;

namespace JMMServer.Providers.TraktTV.Contracts
{
    [DataContract(Name = "show")]
    public class TraktV2Show
    {
        [DataMember(Name = "title")]
        public string Title { get; set; }

        [DataMember(Name = "overview")]
        public string Overview { get; set; }

        [DataMember(Name = "year")]
        public int? Year { get; set; }

        [DataMember(Name = "images")]
        public TraktV2Images Images { get; set; }

        [DataMember(Name = "ids")]
        public TraktV2Ids ids { get; set; }

        public string ShowURL
        {
            get { return string.Format(TraktURIs.WebsiteShow, ids.slug); }
        }

        public override string ToString()
        {
            return string.Format("TraktV2Show: {0}", Title);
        }
    }
}
using System.Runtime.Serialization;
using Shoko.Models.Client;

namespace Shoko.Server.Providers.TraktTV.Contracts
{
    [DataContract]
    public class TraktV2SearchShowResult
    {
        [DataMember(Name = "type")]
        public string type { get; set; }

        [DataMember(Name = "score")]
        public float score { get; set; }

        [DataMember(Name = "show")]
        public TraktV2Show show { get; set; }

        public override string ToString()
        {
            return string.Format("{0} - {1} - {2}", show.Title, show.Year, show.Overview);
        }

        public string ShowURL
        {
            get { return string.Format(TraktURIs.WebsiteShow, show.ids.slug); }
        }


        public CL_TraktTVShowResponse ToContract()
        {
            CL_TraktTVShowResponse contract = new CL_TraktTVShowResponse
            {
                title = show.Title,
                year = show.Year.ToString(),
                url = ShowURL,
                first_aired = string.Empty,
                country = string.Empty,
                overview = show.Overview,
                tvdb_id = show.ids.tvdb.ToString()
            };
            return contract;
        }
    }
}
using System.Collections.Generic;

namespace Shoko.Models
{
    public class Contract_Trakt_Show
    {
        public int Trakt_ShowID { get; set; }
        public string TraktID { get; set; }
        public string Title { get; set; }
        public string Year { get; set; }
        public string URL { get; set; }
        public string Overview { get; set; }
        public int? TvDB_ID { get; set; }
        public List<Contract_Trakt_Season> Seasons { get; set; }
    }
}
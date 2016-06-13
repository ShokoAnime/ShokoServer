using System.Collections.Generic;

namespace JMMContracts
{
    public class Contract_AnimeSearch
    {
        public int AnimeID { get; set; }
        public string MainTitle { get; set; }
        public HashSet<string> Titles { get; set; }
        public bool SeriesExists { get; set; }
        public int? AnimeSeriesID { get; set; }
        public string AnimeSeriesName { get; set; }
        public int? AnimeGroupID { get; set; }
        public string AnimeGroupName { get; set; }

        public override string ToString()
        {
            return string.Format("{0} - {1} - {2}", AnimeID, MainTitle, Titles);
        }
    }
}
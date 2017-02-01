using System.Collections.Generic;
using Shoko.Models.Enums;
using Shoko.Models.Server;


namespace Shoko.Commons.Downloads
{
    public class DownloadSearchCriteria
    {
        private DownloadSearchType searchType = DownloadSearchType.Manual;
        public DownloadSearchType SearchType
        {
            get { return searchType; }
            set { searchType = value; }
        }

        public AniDB_Anime Anime { get; set; }
        public AnimeEpisode_User Episode { get; set; }


        private List<string> searchParameter = null;
        public List<string> SearchParameter
        {
            get { return searchParameter; }
            set { searchParameter = value; }
        }

        public DownloadSearchCriteria(DownloadSearchType sType, List<string> searchparams, AniDB_Anime anime, AnimeEpisode_User ep)
        {
            searchType = sType;
            searchParameter = searchparams;
            Anime = anime;
            Episode = ep;
        }
        public override string ToString()
        {
            string ret = "";

            switch (searchType)
            {
                case DownloadSearchType.Episode: ret = "Episode"; break;
                case DownloadSearchType.Manual: ret = "Manual"; break;
                case DownloadSearchType.Series: ret = "Anime"; break;
            }

            ret += ": ";

            int i = 0;
            List<string> parms = SearchParameter;
            foreach (string parm in parms)
            {
                i++;
                ret += parm;
                if (i < parms.Count) ret += " + ";
            }

            return ret;
        }
    }
}

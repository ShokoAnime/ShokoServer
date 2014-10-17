using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JMMContracts;

namespace JMMServer.Entities
{
    public class CrossRef_AniDB_Trakt_Episode
    {
        public int CrossRef_AniDB_Trakt_EpisodeID { get; private set; }
        public int AnimeID { get; set; }
        public int AniDBEpisodeID { get; set; }
        public string TraktID { get; set; }
        public int Season { get; set; }
        public int EpisodeNumber { get; set; }

        public Contract_CrossRef_AniDB_Trakt_Episode ToContract()
        {
            Contract_CrossRef_AniDB_Trakt_Episode contract = new Contract_CrossRef_AniDB_Trakt_Episode();
            contract.AnimeID = this.AnimeID;
            contract.AniDBEpisodeID = this.AniDBEpisodeID;
            contract.CrossRef_AniDB_Trakt_EpisodeID = this.CrossRef_AniDB_Trakt_EpisodeID;
            contract.TraktID = this.TraktID;
            contract.Season = this.Season;
            contract.EpisodeNumber = this.EpisodeNumber;

            return contract;
        }
    }
}

using System.Collections.Generic;
using Shoko.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.Entities
{
    public class Trakt_Season
    {
        public int Trakt_SeasonID { get; private set; }
        public int Trakt_ShowID { get; set; }
        public int Season { get; set; }
        public string URL { get; set; }

        public List<Trakt_Episode> Episodes => RepoFactory.Trakt_Episode.GetByShowIDAndSeason(Trakt_ShowID, Season);

        public Contract_Trakt_Season ToContract()
        {
            Contract_Trakt_Season contract = new Contract_Trakt_Season();

            contract.Trakt_SeasonID = Trakt_SeasonID;
            contract.Trakt_ShowID = Trakt_ShowID;
            contract.Season = Season;
            contract.URL = URL;
            contract.Episodes = new List<Contract_Trakt_Episode>();

            foreach (Trakt_Episode ep in Episodes)
                contract.Episodes.Add(ep.ToContract());

            return contract;
        }
    }
}
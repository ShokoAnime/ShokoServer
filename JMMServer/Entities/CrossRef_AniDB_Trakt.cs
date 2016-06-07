using JMMContracts;
using JMMServer.Repositories;
using NHibernate;

namespace JMMServer.Entities
{
    public class CrossRef_AniDB_Trakt
    {
        public int CrossRef_AniDB_TraktID { get; private set; }
        public int AnimeID { get; set; }
        public string TraktID { get; set; }
        public int TraktSeasonNumber { get; set; }
        public int CrossRefSource { get; set; }

        public Trakt_Show GetByTraktShow()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByTraktShow(session);
            }
        }

        public Trakt_Show GetByTraktShow(ISession session)
        {
            var repTraktShows = new Trakt_ShowRepository();
            return repTraktShows.GetByTraktSlug(session, TraktID);
        }

        public Contract_CrossRef_AniDB_Trakt ToContract()
        {
            var contract = new Contract_CrossRef_AniDB_Trakt();

            contract.CrossRef_AniDB_TraktID = CrossRef_AniDB_TraktID;
            contract.AnimeID = AnimeID;
            contract.TraktID = TraktID;
            contract.TraktSeasonNumber = TraktSeasonNumber;
            contract.CrossRefSource = CrossRefSource;

            return contract;
        }
    }
}
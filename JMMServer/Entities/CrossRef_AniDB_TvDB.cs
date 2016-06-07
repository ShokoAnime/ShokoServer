using JMMContracts;
using JMMServer.Repositories;
using NHibernate;

namespace JMMServer.Entities
{
    public class CrossRef_AniDB_TvDB
    {
        public int CrossRef_AniDB_TvDBID { get; private set; }
        public int AnimeID { get; set; }
        public int TvDBID { get; set; }
        public int TvDBSeasonNumber { get; set; }
        public int CrossRefSource { get; set; }


        public TvDB_Series GetTvDBSeries()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetTvDBSeries(session);
            }
        }

        public TvDB_Series GetTvDBSeries(ISession session)
        {
            var repTvSeries = new TvDB_SeriesRepository();
            return repTvSeries.GetByTvDBID(session, TvDBID);
        }

        public Contract_CrossRef_AniDB_TvDB ToContract()
        {
            var contract = new Contract_CrossRef_AniDB_TvDB();
            contract.AnimeID = AnimeID;
            contract.TvDBID = TvDBID;
            contract.CrossRef_AniDB_TvDBID = CrossRef_AniDB_TvDBID;
            contract.TvDBSeasonNumber = TvDBSeasonNumber;
            contract.CrossRefSource = CrossRefSource;
            return contract;
        }
    }
}
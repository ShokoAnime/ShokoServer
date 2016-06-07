using JMMContracts;
using JMMServer.Repositories;
using NHibernate;

namespace JMMServer.Entities
{
    public class CrossRef_AniDB_TvDBV2
    {
        public int CrossRef_AniDB_TvDBV2ID { get; private set; }
        public int AnimeID { get; set; }
        public int AniDBStartEpisodeType { get; set; }
        public int AniDBStartEpisodeNumber { get; set; }
        public int TvDBID { get; set; }
        public int TvDBSeasonNumber { get; set; }
        public int TvDBStartEpisodeNumber { get; set; }
        public string TvDBTitle { get; set; }

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

        public Contract_CrossRef_AniDB_TvDBV2 ToContract()
        {
            var contract = new Contract_CrossRef_AniDB_TvDBV2();
            contract.CrossRef_AniDB_TvDBV2ID = CrossRef_AniDB_TvDBV2ID;
            contract.AnimeID = AnimeID;
            contract.AniDBStartEpisodeType = AniDBStartEpisodeType;
            contract.AniDBStartEpisodeNumber = AniDBStartEpisodeNumber;
            contract.TvDBID = TvDBID;
            contract.TvDBSeasonNumber = TvDBSeasonNumber;
            contract.TvDBStartEpisodeNumber = TvDBStartEpisodeNumber;
            contract.CrossRefSource = CrossRefSource;

            contract.TvDBTitle = TvDBTitle;


            return contract;
        }
    }
}
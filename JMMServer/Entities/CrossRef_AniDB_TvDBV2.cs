using System;
using JMMContracts;
using JMMServer.Repositories;
using JMMServer.Repositories.NHibernate;
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
                return GetTvDBSeries(session.Wrap());
            }
        }

        public TvDB_Series GetTvDBSeries(ISessionWrapper session)
        {
            TvDB_SeriesRepository repTvSeries = new TvDB_SeriesRepository();
            return repTvSeries.GetByTvDBID(session, TvDBID);
        }

        public Contract_CrossRef_AniDB_TvDBV2 ToContract()
        {
            Contract_CrossRef_AniDB_TvDBV2 contract = new Contract_CrossRef_AniDB_TvDBV2();
            contract.CrossRef_AniDB_TvDBV2ID = this.CrossRef_AniDB_TvDBV2ID;
            contract.AnimeID = this.AnimeID;
            contract.AniDBStartEpisodeType = this.AniDBStartEpisodeType;
            contract.AniDBStartEpisodeNumber = this.AniDBStartEpisodeNumber;
            contract.TvDBID = this.TvDBID;
            contract.TvDBSeasonNumber = this.TvDBSeasonNumber;
            contract.TvDBStartEpisodeNumber = this.TvDBStartEpisodeNumber;
            contract.CrossRefSource = this.CrossRefSource;

            contract.TvDBTitle = this.TvDBTitle;


            return contract;
        }
    }
}
using JMMContracts;
using JMMServer.Repositories;
using NHibernate;

namespace JMMServer.Entities
{
    public class CrossRef_AniDB_TraktV2
    {
        public int CrossRef_AniDB_TraktV2ID { get; private set; }
        public int AnimeID { get; set; }
        public int AniDBStartEpisodeType { get; set; }
        public int AniDBStartEpisodeNumber { get; set; }
        public string TraktID { get; set; }
        public int TraktSeasonNumber { get; set; }
        public int TraktStartEpisodeNumber { get; set; }
        public string TraktTitle { get; set; }

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

        public Contract_CrossRef_AniDB_TraktV2 ToContract()
        {
            var contract = new Contract_CrossRef_AniDB_TraktV2();
            contract.CrossRef_AniDB_TraktV2ID = CrossRef_AniDB_TraktV2ID;
            contract.AnimeID = AnimeID;
            contract.AniDBStartEpisodeType = AniDBStartEpisodeType;
            contract.AniDBStartEpisodeNumber = AniDBStartEpisodeNumber;
            contract.TraktID = TraktID;
            contract.TraktSeasonNumber = TraktSeasonNumber;
            contract.TraktStartEpisodeNumber = TraktStartEpisodeNumber;
            contract.CrossRefSource = CrossRefSource;

            contract.TraktTitle = TraktTitle;


            return contract;
        }
    }
}
using JMMContracts;
using JMMServer.Databases;
using JMMServer.Repositories;
using JMMServer.Repositories.Direct;
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
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetByTraktShow(session);
            }
        }

        public Trakt_Show GetByTraktShow(ISession session)
        {
            return RepoFactory.Trakt_Show.GetByTraktSlug(session, TraktID);
        }

        public Contract_CrossRef_AniDB_TraktV2 ToContract()
        {
            Contract_CrossRef_AniDB_TraktV2 contract = new Contract_CrossRef_AniDB_TraktV2();
            contract.CrossRef_AniDB_TraktV2ID = this.CrossRef_AniDB_TraktV2ID;
            contract.AnimeID = this.AnimeID;
            contract.AniDBStartEpisodeType = this.AniDBStartEpisodeType;
            contract.AniDBStartEpisodeNumber = this.AniDBStartEpisodeNumber;
            contract.TraktID = this.TraktID;
            contract.TraktSeasonNumber = this.TraktSeasonNumber;
            contract.TraktStartEpisodeNumber = this.TraktStartEpisodeNumber;
            contract.CrossRefSource = this.CrossRefSource;

            contract.TraktTitle = this.TraktTitle;


            return contract;
        }
    }
}
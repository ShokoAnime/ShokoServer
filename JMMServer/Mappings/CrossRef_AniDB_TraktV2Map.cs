using FluentNHibernate.Mapping;
using JMMServer.Entities;
using Shoko.Models.Server;

namespace JMMServer.Mappings
{
    public class CrossRef_AniDB_TraktV2Map : ClassMap<SVR_CrossRef_AniDB_TraktV2>
    {
        public CrossRef_AniDB_TraktV2Map()
        {
            Not.LazyLoad();
            Id(x => x.CrossRef_AniDB_TraktV2ID);

            Map(x => x.AnimeID).Not.Nullable();
            Map(x => x.CrossRefSource).Not.Nullable();
            Map(x => x.TraktID);
            Map(x => x.TraktSeasonNumber).Not.Nullable();
            Map(x => x.AniDBStartEpisodeType).Not.Nullable();
            Map(x => x.AniDBStartEpisodeNumber).Not.Nullable();
            Map(x => x.TraktStartEpisodeNumber).Not.Nullable();
            Map(x => x.TraktTitle);
        }
    }
}
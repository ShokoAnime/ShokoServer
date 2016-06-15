using FluentNHibernate.Mapping;
using JMMServer.Entities;

namespace JMMServer.Mappings
{
    public class CrossRef_AniDB_MALMap : ClassMap<CrossRef_AniDB_MAL>
    {
        public CrossRef_AniDB_MALMap()
        {
            Not.LazyLoad();
            Id(x => x.CrossRef_AniDB_MALID);

            Map(x => x.AnimeID).Not.Nullable();
            Map(x => x.CrossRefSource).Not.Nullable();
            Map(x => x.MALID).Not.Nullable();
            Map(x => x.MALTitle);
            Map(x => x.StartEpisodeType).Not.Nullable();
            Map(x => x.StartEpisodeNumber).Not.Nullable();
        }
    }
}
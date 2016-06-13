using FluentNHibernate.Mapping;
using JMMServer.Entities;

namespace JMMServer.Mappings
{
    public class CrossRef_AniDB_OtherMap : ClassMap<CrossRef_AniDB_Other>
    {
        public CrossRef_AniDB_OtherMap()
        {
            Not.LazyLoad();
            Id(x => x.CrossRef_AniDB_OtherID);

            Map(x => x.AnimeID).Not.Nullable();
            Map(x => x.CrossRefID);
            Map(x => x.CrossRefSource).Not.Nullable();
            Map(x => x.CrossRefType).Not.Nullable();
        }
    }
}
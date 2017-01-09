using FluentNHibernate.Mapping;
using JMMServer.Entities;
using Shoko.Models.Server;

namespace JMMServer.Mappings
{
    public class CrossRef_AniDB_OtherMap : ClassMap<SVR_CrossRef_AniDB_Other>
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
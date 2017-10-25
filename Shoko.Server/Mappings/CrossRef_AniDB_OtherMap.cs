using FluentNHibernate.Mapping;
using Shoko.Models.Server;
using Shoko.Server.Models;

namespace Shoko.Server.Mappings
{
    public class CrossRef_AniDB_OtherMap : ClassMap<CrossRef_AniDB_Other>
    {
        public CrossRef_AniDB_OtherMap()
        {
            Table("CrossRef_AniDB_Other");

            Not.LazyLoad();
            Id(x => x.CrossRef_AniDB_OtherID);

            Map(x => x.AnimeID).Not.Nullable();
            Map(x => x.CrossRefID);
            Map(x => x.CrossRefSource).Not.Nullable();
            Map(x => x.CrossRefType).Not.Nullable();
        }
    }
}
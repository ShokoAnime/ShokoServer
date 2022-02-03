using FluentNHibernate.Mapping;
using Shoko.Models.Enums;
using Shoko.Models.Server;

namespace Shoko.Server.Mappings
{
    public class CrossRef_AniDB_TvDBMap : ClassMap<CrossRef_AniDB_TvDB>
    {
        public CrossRef_AniDB_TvDBMap()
        {
            Not.LazyLoad();
            Id(x => x.CrossRef_AniDB_TvDBID);

            Map(x => x.AniDBID).Not.Nullable();
            Map(x => x.TvDBID).Not.Nullable();
            Map(x => x.CrossRefSource).CustomType<CrossRefSource>().Not.Nullable();
        }
    }
}

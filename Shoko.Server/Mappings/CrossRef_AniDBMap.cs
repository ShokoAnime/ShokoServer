using FluentNHibernate.Mapping;
using Shoko.Models.Enums;
using Shoko.Models.Server;

namespace Shoko.Server.Mappings
{
    public class CrossRef_AniDBMap : ClassMap<CrossRef_AniDB>
    {
        public CrossRef_AniDBMap()
        {
            Not.LazyLoad();
            Id(x => x.CrossRef_AniDBID);
            Map(x => x.AniDBID).Not.Nullable();
            Map(x => x.ProviderID).Not.Nullable();
            Map(x => x.Provider).Not.Nullable();
            Map(x => x.ProviderMediaType).CustomType<MediaType>().Not.Nullable();
            Map(x => x.CrossRefSource).CustomType<CrossRefSource>().Not.Nullable();
        }
    }
}

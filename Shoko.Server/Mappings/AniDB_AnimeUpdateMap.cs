using FluentNHibernate.Mapping;
using Shoko.Server.Models;

namespace Shoko.Server.Mappings
{
    public class AniDB_AnimeUpdateMap : ClassMap<AniDB_AnimeUpdate>
    {
        public AniDB_AnimeUpdateMap()
        {
            Table("AniDB_AnimeUpdate");
            Not.LazyLoad();
            Id(x => x.AniDB_AnimeUpdateID);
            Map(x => x.AnimeID).Not.Nullable();
            Map(x => x.UpdatedAt).Not.Nullable();
        }
    }
}
using FluentNHibernate.Mapping;
using Shoko.Models.Server;

namespace Shoko.Server.Mappings;

public class AniDB_Anime_StaffMap : ClassMap<AniDB_Anime_Staff>
{
    public AniDB_Anime_StaffMap()
    {
        Table("AniDB_Anime_Staff");
        Not.LazyLoad();
        Id(x => x.AniDB_Anime_StaffID);
        Map(x => x.AnimeID).Not.Nullable();
        Map(x => x.CreatorID).Not.Nullable();
        Map(x => x.CreatorType).Not.Nullable();
    }
}

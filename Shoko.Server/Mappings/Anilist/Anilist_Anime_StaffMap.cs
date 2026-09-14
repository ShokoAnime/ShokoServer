using FluentNHibernate.Mapping;
using Shoko.Server.Models.Anilist;

namespace Shoko.Server.Mappings.Anilist;

public class Anilist_Anime_StaffMap : ClassMap<Anilist_Anime_Staff>
{
    public Anilist_Anime_StaffMap()
    {
        Table("Anilist_Anime_Staff");

        Not.LazyLoad();
        Id(x => x.Anilist_Anime_StaffID);

        Map(x => x.AnilistAnimeID).Not.Nullable();
        Map(x => x.AnilistCreatorID).Not.Nullable();
        Map(x => x.Role).Not.Nullable();
        Map(x => x.Ordering).Not.Nullable();
    }
}

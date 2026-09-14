using FluentNHibernate.Mapping;
using Shoko.Server.Models.Anilist;

namespace Shoko.Server.Mappings.Anilist;

public class Anilist_Anime_Character_CreatorMap : ClassMap<Anilist_Anime_Character_Creator>
{
    public Anilist_Anime_Character_CreatorMap()
    {
        Table("Anilist_Anime_Character_Creator");

        Not.LazyLoad();
        Id(x => x.Anilist_Anime_Character_CreatorID);

        Map(x => x.AnilistAnimeID).Not.Nullable();
        Map(x => x.AnilistCharacterID).Not.Nullable();
        Map(x => x.AnilistCreatorID).Not.Nullable();
        Map(x => x.RoleNotes);
        Map(x => x.DubGroup);
        Map(x => x.Ordering).Not.Nullable();
    }
}

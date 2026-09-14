using FluentNHibernate.Mapping;
using Shoko.Server.Models.Anilist;

namespace Shoko.Server.Mappings.Anilist;

public class Anilist_Anime_CharacterMap : ClassMap<Anilist_Anime_Character>
{
    public Anilist_Anime_CharacterMap()
    {
        Table("Anilist_Anime_Character");

        Not.LazyLoad();
        Id(x => x.Anilist_Anime_CharacterID);

        Map(x => x.AnilistAnimeID).Not.Nullable();
        Map(x => x.AnilistCharacterID).Not.Nullable();
        Map(x => x.Role).Not.Nullable();
        Map(x => x.Ordering).Not.Nullable();
    }
}

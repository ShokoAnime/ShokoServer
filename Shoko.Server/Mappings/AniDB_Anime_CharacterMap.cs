using FluentNHibernate.Mapping;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Server;

namespace Shoko.Server.Mappings;

public class AniDB_Anime_CharacterMap : ClassMap<AniDB_Anime_Character>
{
    public AniDB_Anime_CharacterMap()
    {
        Table("AniDB_Anime_Character");
        Not.LazyLoad();
        Id(x => x.AniDB_Anime_CharacterID);
        Map(x => x.AnimeID).Not.Nullable();
        Map(x => x.CharacterID).Not.Nullable();
        Map(x => x.Appearance).Not.Nullable();
        Map(x => x.AppearanceType).CustomType<CharacterAppearanceType>().Not.Nullable();
        Map(x => x.Ordering).Not.Nullable();
    }
}

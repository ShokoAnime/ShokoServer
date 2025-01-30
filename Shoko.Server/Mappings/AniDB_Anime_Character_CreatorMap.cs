using FluentNHibernate.Mapping;
using Shoko.Server.Models.AniDB;

namespace Shoko.Server.Mappings;

public class AniDB_Anime_Character_CreatorMap : ClassMap<AniDB_Anime_Character_Creator>
{
    public AniDB_Anime_Character_CreatorMap()
    {
        Table("AniDB_Anime_Character_Creator");
        Not.LazyLoad();
        Id(x => x.AniDB_Anime_Character_CreatorID);

        Map(x => x.AnimeID).Not.Nullable();
        Map(x => x.CharacterID).Not.Nullable();
        Map(x => x.CreatorID).Not.Nullable();
        Map(x => x.Ordering).Not.Nullable();
    }
}

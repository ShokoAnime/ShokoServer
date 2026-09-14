using FluentNHibernate.Mapping;
using Shoko.Server.Models.Anilist;

namespace Shoko.Server.Mappings.Anilist;

public class Anilist_Anime_TagMap : ClassMap<Anilist_Anime_Tag>
{
    public Anilist_Anime_TagMap()
    {
        Table("Anilist_Anime_Tag");

        Not.LazyLoad();
        Id(x => x.Anilist_Anime_TagID);

        Map(x => x.AnilistAnimeID).Not.Nullable();
        Map(x => x.AnilistTagID).Not.Nullable();
        Map(x => x.Weight).Not.Nullable();
        Map(x => x.IsLocalSpoiler).Not.Nullable();
    }
}

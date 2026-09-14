using FluentNHibernate.Mapping;
using Shoko.Server.Models.Anilist;

namespace Shoko.Server.Mappings.Anilist;

public class Anilist_Anime_RelationMap : ClassMap<Anilist_Anime_Relation>
{
    public Anilist_Anime_RelationMap()
    {
        Table("Anilist_Anime_Relation");

        Not.LazyLoad();
        Id(x => x.Anilist_Anime_RelationID);

        Map(x => x.AnilistAnimeID).Not.Nullable();
        Map(x => x.RelatedAnilistID).Not.Nullable();
        Map(x => x.RelatedIsAnime).Not.Nullable();
        Map(x => x.RelationType).Not.Nullable();
    }
}

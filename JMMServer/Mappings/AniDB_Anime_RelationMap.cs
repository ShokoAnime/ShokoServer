using FluentNHibernate.Mapping;
using JMMServer.Entities;

namespace JMMServer.Mappings
{
    public class AniDB_Anime_RelationMap : ClassMap<AniDB_Anime_Relation>
    {
        public AniDB_Anime_RelationMap()
        {
            Not.LazyLoad();
            Id(x => x.AniDB_Anime_RelationID);

            Map(x => x.AnimeID).Not.Nullable();
            Map(x => x.RelatedAnimeID).Not.Nullable();
            Map(x => x.RelationType).Not.Nullable();
        }
    }
}
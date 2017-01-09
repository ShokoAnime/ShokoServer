using FluentNHibernate.Mapping;
using Shoko.Models.Server;
using Shoko.Server.Entities;

namespace Shoko.Server.Mappings
{
    public class AniDB_Anime_RelationMap : ClassMap<SVR_AniDB_Anime_Relation>
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
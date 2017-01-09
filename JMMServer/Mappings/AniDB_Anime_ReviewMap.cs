using FluentNHibernate.Mapping;
using Shoko.Models.Server;

namespace JMMServer.Mappings
{
    public class AniDB_Anime_ReviewMap : ClassMap<AniDB_Anime_Review>
    {
        public AniDB_Anime_ReviewMap()
        {
            Not.LazyLoad();
            Id(x => x.AniDB_Anime_ReviewID);

            Map(x => x.AnimeID).Not.Nullable();
            Map(x => x.ReviewID).Not.Nullable();
        }
    }
}
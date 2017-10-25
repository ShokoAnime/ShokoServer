using FluentNHibernate.Mapping;
using Shoko.Models.Server;
using Shoko.Server.Models;

namespace Shoko.Server.Mappings
{
    public class AniDB_Anime_SimilarMap : ClassMap<AniDB_Anime_Similar>
    {
        public AniDB_Anime_SimilarMap()
        {
            Table("AniDB_Anime_Similar");
            Not.LazyLoad();
            Id(x => x.AniDB_Anime_SimilarID);

            Map(x => x.AnimeID).Not.Nullable();
            Map(x => x.Approval).Not.Nullable();
            Map(x => x.SimilarAnimeID).Not.Nullable();
            Map(x => x.Total).Not.Nullable();
        }
    }
}
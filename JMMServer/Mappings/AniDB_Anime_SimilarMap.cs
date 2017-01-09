using FluentNHibernate.Mapping;
using JMMServer.Entities;
using Shoko.Models.Server;

namespace JMMServer.Mappings
{
    public class AniDB_Anime_SimilarMap : ClassMap<SVR_AniDB_Anime_Similar>
    {
        public AniDB_Anime_SimilarMap()
        {
            Not.LazyLoad();
            Id(x => x.AniDB_Anime_SimilarID);

            Map(x => x.AnimeID).Not.Nullable();
            Map(x => x.Approval).Not.Nullable();
            Map(x => x.SimilarAnimeID).Not.Nullable();
            Map(x => x.Total).Not.Nullable();
        }
    }
}
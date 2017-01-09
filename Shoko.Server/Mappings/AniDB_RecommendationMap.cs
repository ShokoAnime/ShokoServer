using FluentNHibernate.Mapping;
using Shoko.Models.Server;
using Shoko.Server.Entities;

namespace Shoko.Server.Mappings
{
    public class AniDB_RecommendationMap : ClassMap<SVR_AniDB_Recommendation>
    {
        public AniDB_RecommendationMap()
        {
            Not.LazyLoad();
            Id(x => x.AniDB_RecommendationID);

            Map(x => x.AnimeID).Not.Nullable();
            Map(x => x.UserID).Not.Nullable();
            Map(x => x.RecommendationType).Not.Nullable();
            Map(x => x.RecommendationText);
        }
    }
}
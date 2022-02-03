using FluentNHibernate.Mapping;
using Shoko.Models.Server;

namespace Shoko.Server.Mappings
{
    public class AniDB_RecommendationMap : ClassMap<AniDB_Recommendation>
    {
        public AniDB_RecommendationMap()
        {
            Table("AniDB_Recommendation");
            Not.LazyLoad();
            Id(x => x.AniDB_RecommendationID);

            Map(x => x.AnimeID).Not.Nullable();
            Map(x => x.UserID).Not.Nullable();
            Map(x => x.RecommendationType).Not.Nullable();
            Map(x => x.RecommendationText);
        }
    }
}
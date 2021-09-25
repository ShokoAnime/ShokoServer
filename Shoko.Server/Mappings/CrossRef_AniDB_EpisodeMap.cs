using FluentNHibernate.Mapping;
using Shoko.Models.Enums;
using Shoko.Models.Server;

namespace Shoko.Server.Mappings
{
    public class CrossRef_AniDB_EpisodeMap : ClassMap<CrossRef_AniDB_Episode>
    {
        public CrossRef_AniDB_EpisodeMap()
        {
            Not.LazyLoad();
            Id(x => x.CrossRef_AniDB_EpisodeID);
            Map(x => x.AniDBEpisodeID).Not.Nullable();
            Map(x => x.ProviderEpisodeID).Not.Nullable();
            Map(x => x.Provider).Not.Nullable();
            Map(x => x.MatchRating).CustomType<MatchRating>().Not.Nullable();
        }
    }
}

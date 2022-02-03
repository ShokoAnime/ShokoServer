using FluentNHibernate.Mapping;
using Shoko.Models.Server;

namespace Shoko.Server.Mappings
{
    public class CrossRef_AniDB_TvDB_Episode_OverrideMap : ClassMap<CrossRef_AniDB_TvDB_Episode_Override>
    {
        public CrossRef_AniDB_TvDB_Episode_OverrideMap()
        {
            Not.LazyLoad();
            Id(x => x.CrossRef_AniDB_TvDB_Episode_OverrideID);

            Map(x => x.AniDBEpisodeID).Not.Nullable();
            Map(x => x.TvDBEpisodeID).Not.Nullable();
        }
    }
}

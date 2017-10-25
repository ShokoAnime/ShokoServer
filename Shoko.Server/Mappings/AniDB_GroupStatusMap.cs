using FluentNHibernate.Mapping;
using Shoko.Models.Server;
using Shoko.Server.Models;

namespace Shoko.Server.Mappings
{
    public class AniDB_GroupStatusMap : ClassMap<AniDB_GroupStatus>
    {
        public AniDB_GroupStatusMap()
        {
            Table("AniDB_GroupStatus");
            Not.LazyLoad();
            Id(x => x.AniDB_GroupStatusID);

            Map(x => x.AnimeID).Not.Nullable();
            Map(x => x.CompletionState).Not.Nullable();
            Map(x => x.EpisodeRange);
            Map(x => x.GroupID).Not.Nullable();
            Map(x => x.GroupName);
            Map(x => x.LastEpisodeNumber).Not.Nullable();
            Map(x => x.Rating).Not.Nullable();
            Map(x => x.Votes).Not.Nullable();
        }
    }
}
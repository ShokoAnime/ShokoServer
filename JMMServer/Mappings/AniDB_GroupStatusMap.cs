using FluentNHibernate.Mapping;
using JMMServer.Entities;
using Shoko.Models.Server;

namespace JMMServer.Mappings
{
    public class AniDB_GroupStatusMap : ClassMap<SVR_AniDB_GroupStatus>
    {
        public AniDB_GroupStatusMap()
        {
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
using FluentNHibernate.Mapping;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.AniDB;

namespace Shoko.Server.Mappings;

public class AniDB_GroupStatusMap : ClassMap<AniDB_GroupStatus>
{
    public AniDB_GroupStatusMap()
    {
        Table("AniDB_GroupStatus");
        Not.LazyLoad();
        Id(x => x.AniDB_GroupStatusID);

        Map(x => x.AnimeID).Not.Nullable();
        Map(x => x.CompletionState).Not.Nullable();
        Map(x => x.EpisodeRange).CustomType<PooledStringType>();
        Map(x => x.GroupID).Not.Nullable();
        Map(x => x.GroupName).CustomType<PooledStringType>();
        Map(x => x.LastEpisodeNumber).Not.Nullable();
        Map(x => x.Rating).Not.Nullable();
        Map(x => x.Votes).Not.Nullable();
    }
}

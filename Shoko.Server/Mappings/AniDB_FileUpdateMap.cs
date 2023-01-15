using FluentNHibernate.Mapping;
using Shoko.Server.Models;

namespace Shoko.Server.Mappings;

public class AniDB_FileUpdateMap : ClassMap<AniDB_FileUpdate>
{
    public AniDB_FileUpdateMap()
    {
        Table("AniDB_FileUpdate");
        Not.LazyLoad();
        Id(x => x.AniDB_FileUpdateID);
        Map(x => x.FileSize).Not.Nullable();
        Map(x => x.Hash).Not.Nullable();
        Map(x => x.HasResponse).Not.Nullable();
        Map(x => x.UpdatedAt).Not.Nullable();
    }
}

using FluentNHibernate.Mapping;
using Shoko.Server.Databases.NHIbernate;
using Shoko.Server.Models.AniDB;

namespace Shoko.Server.Mappings;

public class AniDB_TagMap : ClassMap<AniDB_Tag>
{
    public AniDB_TagMap()
    {
        Table("AniDB_Tag");
        Not.LazyLoad();
        Id(x => x.AniDB_TagID);

        Map(x => x.TagID).Not.Nullable();
        Map(x => x.ParentTagID);
        Map(x => x.TagNameSource).Column("TagName").Not.Nullable();
        Map(x => x.TagNameOverride);
        Map(x => x.TagDescription).Not.Nullable().CustomType("StringClob");
        Map(x => x.GlobalSpoiler).CustomType<BoolToIntConverter>().Not.Nullable();
        Map(x => x.Verified).CustomType<BoolToIntConverter>().Not.Nullable();
        Map(x => x.LastUpdated);
    }
}

using FluentNHibernate.Mapping;
using Shoko.Models.Server;

namespace Shoko.Server.Mappings
{
    public class AniDB_TagMap : ClassMap<AniDB_Tag>
    {
        public AniDB_TagMap()
        {
            Table("AniDB_Tag");
            Not.LazyLoad();
            Id(x => x.AniDB_TagID);

            Map(x => x.GlobalSpoiler).Not.Nullable();
            Map(x => x.LocalSpoiler).Not.Nullable();
            Map(x => x.Spoiler).Not.Nullable();
            Map(x => x.TagCount).Not.Nullable();
            Map(x => x.TagDescription).Not.Nullable();
            Map(x => x.TagID).Not.Nullable();
            Map(x => x.TagName).Not.Nullable();
        }
    }
}
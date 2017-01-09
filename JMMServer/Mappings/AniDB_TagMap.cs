using FluentNHibernate.Mapping;
using JMMServer.Entities;
using Shoko.Models.Server;

namespace JMMServer.Mappings
{
    public class AniDB_TagMap : ClassMap<SVR_AniDB_Tag>
    {
        public AniDB_TagMap()
        {
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
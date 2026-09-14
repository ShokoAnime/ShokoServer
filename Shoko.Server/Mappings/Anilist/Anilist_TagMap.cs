using FluentNHibernate.Mapping;
using Shoko.Server.Models.Anilist;

namespace Shoko.Server.Mappings.Anilist;

public class Anilist_TagMap : ClassMap<Anilist_Tag>
{
    public Anilist_TagMap()
    {
        Table("Anilist_Tag");

        Not.LazyLoad();
        Id(x => x.Anilist_TagID);

        Map(x => x.AnilistTagID).Not.Nullable();
        Map(x => x.Name).Not.Nullable();
        Map(x => x.Description).Not.Nullable();
        Map(x => x.Category).Not.Nullable();
        Map(x => x.IsRestricted).Not.Nullable();
        Map(x => x.IsSpoiler).Not.Nullable();
        Map(x => x.LastUpdatedAt).Not.Nullable();
    }
}

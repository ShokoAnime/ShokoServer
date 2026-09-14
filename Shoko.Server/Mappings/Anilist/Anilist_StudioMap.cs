using FluentNHibernate.Mapping;
using Shoko.Server.Models.Anilist;

namespace Shoko.Server.Mappings.Anilist;

public class Anilist_StudioMap : ClassMap<Anilist_Studio>
{
    public Anilist_StudioMap()
    {
        Table("Anilist_Studio");

        Not.LazyLoad();
        Id(x => x.Anilist_StudioID);

        Map(x => x.AnilistStudioID).Not.Nullable();
        Map(x => x.Name).Not.Nullable();
        Map(x => x.IsAnimationStudio).Not.Nullable();
        Map(x => x.FavoriteCount).Not.Nullable();
        Map(x => x.LastUpdatedAt).Not.Nullable();
    }
}

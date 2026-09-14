using FluentNHibernate.Mapping;
using Shoko.Server.Models.Anilist;

namespace Shoko.Server.Mappings.Anilist;

public class Anilist_Anime_StudioMap : ClassMap<Anilist_Anime_Studio>
{
    public Anilist_Anime_StudioMap()
    {
        Table("Anilist_Anime_Studio");

        Not.LazyLoad();
        Id(x => x.Anilist_Anime_StudioID);

        Map(x => x.AnilistAnimeID).Not.Nullable();
        Map(x => x.AnilistStudioID).Not.Nullable();
        Map(x => x.IsMainStudio).Not.Nullable();
    }
}

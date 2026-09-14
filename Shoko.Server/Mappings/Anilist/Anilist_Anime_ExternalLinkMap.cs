using FluentNHibernate.Mapping;
using Shoko.Server.Models.Anilist;

namespace Shoko.Server.Mappings.Anilist;

public class Anilist_Anime_ExternalLinkMap : ClassMap<Anilist_Anime_ExternalLink>
{
    public Anilist_Anime_ExternalLinkMap()
    {
        Table("Anilist_Anime_ExternalLink");

        Not.LazyLoad();
        Id(x => x.Anilist_Anime_ExternalLinkID);

        Map(x => x.AnilistAnimeID).Not.Nullable();
        Map(x => x.AnilistLinkID).Not.Nullable();
        Map(x => x.Url).Not.Nullable();
        Map(x => x.Site).Not.Nullable();
        Map(x => x.AnilistSiteID).Nullable();
        Map(x => x.LinkType).Not.Nullable();
        Map(x => x.LanguageCode).Nullable();
    }
}

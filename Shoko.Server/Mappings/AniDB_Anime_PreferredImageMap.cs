using FluentNHibernate.Mapping;
using Shoko.Abstractions.Enums;
using Shoko.Server.Models.AniDB;

namespace Shoko.Server.Mappings;

public class AniDB_Anime_PreferredImageMap : ClassMap<AniDB_Anime_PreferredImage>
{
    public AniDB_Anime_PreferredImageMap()
    {
        Table("AniDB_Anime_PreferredImage");
        Not.LazyLoad();
        Id(x => x.AniDB_Anime_PreferredImageID);

        Map(x => x.AnidbAnimeID).Not.Nullable();
        Map(x => x.ImageID).Not.Nullable();
        Map(x => x.ImageSource).Not.Nullable().CustomType<DataSource>();
        Map(x => x.ImageType).Not.Nullable().CustomType<ImageEntityType>();
    }
}

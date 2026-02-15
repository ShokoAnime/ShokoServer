using FluentNHibernate.Mapping;
using Shoko.Abstractions.Enums;
using Shoko.Server.Models.AniDB;

namespace Shoko.Server.Mappings;

public class AniDB_Episode_PreferredImageMap : ClassMap<AniDB_Episode_PreferredImage>
{
    public AniDB_Episode_PreferredImageMap()
    {
        Table("AniDB_Episode_PreferredImage");
        Not.LazyLoad();
        Id(x => x.AniDB_Episode_PreferredImageID);

        Map(x => x.AnidbAnimeID).Not.Nullable();
        Map(x => x.AnidbEpisodeID).Not.Nullable();
        Map(x => x.ImageID).Not.Nullable();
        Map(x => x.ImageSource).Not.Nullable().CustomType<DataSource>();
        Map(x => x.ImageType).Not.Nullable().CustomType<ImageEntityType>();
    }
}

using FluentNHibernate.Mapping;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Server;

namespace Shoko.Server.Mappings;

public class TMDB_ImageMap : ClassMap<TMDB_Image>
{
    public TMDB_ImageMap()
    {
        Table("TMDB_Image");

        Not.LazyLoad();
        Id(x => x.TMDB_ImageID);

        Map(x => x.TmdbMovieID);
        Map(x => x.TmdbEpisodeID);
        Map(x => x.TmdbSeasonID);
        Map(x => x.TmdbShowID);
        Map(x => x.TmdbCollectionID);
        Map(x => x.TmdbNetworkID);
        Map(x => x.TmdbCompanyID);
        Map(x => x.TmdbPersonID);
        Map(x => x.IsEnabled);
        Map(x => x.ForeignType).Not.Nullable().CustomType<ForeignEntityType>();
        Map(x => x.ImageType).Not.Nullable().CustomType<ImageEntityType>();
        Map(x => x.Width).Not.Nullable();
        Map(x => x.Height).Not.Nullable();
        Map(x => x.Language).Not.Nullable().CustomType<TitleLanguageConverter>();
        Map(x => x.RemoteFileName).Not.Nullable();
        Map(x => x.UserRating).Not.Nullable();
        Map(x => x.UserVotes).Not.Nullable();
    }
}

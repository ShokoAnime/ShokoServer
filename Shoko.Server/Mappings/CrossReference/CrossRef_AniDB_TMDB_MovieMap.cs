using FluentNHibernate.Mapping;
using Shoko.Models.Enums;
using Shoko.Server.Models.CrossReference;

namespace Shoko.Server.Mappings;

public class CrossRef_AniDB_TMDB_MovieMap : ClassMap<CrossRef_AniDB_TMDB_Movie>
{
    public CrossRef_AniDB_TMDB_MovieMap()
    {
        Table("CrossRef_AniDB_TMDB_Movie");

        Not.LazyLoad();
        Id(x => x.CrossRef_AniDB_TMDB_MovieID);

        Map(x => x.AnidbAnimeID).Not.Nullable();
        Map(x => x.AnidbEpisodeID).Not.Nullable();
        Map(x => x.TmdbMovieID).Not.Nullable();
        Map(x => x.Source).CustomType<CrossRefSource>().Not.Nullable();
    }
}

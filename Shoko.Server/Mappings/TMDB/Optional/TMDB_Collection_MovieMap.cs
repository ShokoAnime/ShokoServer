using FluentNHibernate.Mapping;
using Shoko.Server.Models.TMDB;

namespace Shoko.Server.Mappings;

public class TMDB_Collection_MovieMap : ClassMap<TMDB_Collection_Movie>
{
    public TMDB_Collection_MovieMap()
    {
        Table("TMDB_Collection_Movie");

        Not.LazyLoad();
        Id(x => x.TMDB_Collection_MovieID);

        Map(x => x.TmdbCollectionID).Not.Nullable();
        Map(x => x.TmdbMovieID).Not.Nullable();
        Map(x => x.Ordering).Not.Nullable();
    }
}

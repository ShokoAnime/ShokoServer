using FluentNHibernate.Mapping;
using Shoko.Server.Databases.TypeConverters;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Server;

namespace Shoko.Server.Mappings;

public class TMDB_Movie_CastMap : ClassMap<TMDB_Movie_Cast>
{
    public TMDB_Movie_CastMap()
    {
        Table("TMDB_Movie_Cast");

        Not.LazyLoad();
        Id(x => x.TMDB_Movie_CastID);

        Map(x => x.TmdbMovieID).Not.Nullable();
        Map(x => x.TmdbPersonID).Not.Nullable();
        Map(x => x.TmdbCreditID).Not.Nullable();
        Map(x => x.CharacterName).Not.Nullable();
        Map(x => x.IsGuestRole).Not.Nullable();
        Map(x => x.Ordering).Not.Nullable();
    }
}

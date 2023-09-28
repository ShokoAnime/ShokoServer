using FluentNHibernate.Mapping;
using Shoko.Server.Models.TMDB;

namespace Shoko.Server.Mappings;

public class TMDB_Episode_CrewMap : ClassMap<TMDB_Episode_Crew>
{
    public TMDB_Episode_CrewMap()
    {
        Table("TMDB_Episode_Crew");

        Not.LazyLoad();
        Id(x => x.TMDB_Episode_CrewID);

        Map(x => x.TmdbShowID).Not.Nullable();
        Map(x => x.TmdbSeasonID).Not.Nullable();
        Map(x => x.TmdbEpisodeID).Not.Nullable();
        Map(x => x.TmdbPersonID).Not.Nullable();
        Map(x => x.TmdbCreditID).Not.Nullable();
        Map(x => x.Job).Not.Nullable();
        Map(x => x.Department).Not.Nullable();
    }
}

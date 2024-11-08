using FluentNHibernate.Mapping;
using Shoko.Server.Models.TMDB;

namespace Shoko.Server.Mappings;

public class TMDB_Episode_CastMap : ClassMap<TMDB_Episode_Cast>
{
    public TMDB_Episode_CastMap()
    {
        Table("TMDB_Episode_Cast");

        Not.LazyLoad();
        Id(x => x.TMDB_Episode_CastID);

        Map(x => x.TmdbShowID).Not.Nullable();
        Map(x => x.TmdbSeasonID).Not.Nullable();
        Map(x => x.TmdbEpisodeID).Not.Nullable();
        Map(x => x.TmdbPersonID).Not.Nullable();
        Map(x => x.TmdbCreditID).Not.Nullable();
        Map(x => x.CharacterName).Not.Nullable();
        Map(x => x.IsGuestRole).Not.Nullable();
        Map(x => x.Ordering).Not.Nullable();
    }
}

using FluentNHibernate.Mapping;
using Shoko.Models.Enums;
using Shoko.Server.Models.CrossReference;

namespace Shoko.Server.Mappings;

public class CrossRef_AniDB_TMDB_ShowMap : ClassMap<CrossRef_AniDB_TMDB_Show>
{
    public CrossRef_AniDB_TMDB_ShowMap()
    {
        Table("CrossRef_AniDB_TMDB_Show");

        Not.LazyLoad();
        Id(x => x.CrossRef_AniDB_TMDB_ShowID);

        Map(x => x.AnidbAnimeID).Not.Nullable();
        Map(x => x.TmdbShowID).Not.Nullable();
        Map(x => x.Source).CustomType<CrossRefSource>().Not.Nullable();
    }
}

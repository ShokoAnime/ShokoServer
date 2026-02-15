using FluentNHibernate.Mapping;
using Shoko.Server.Models.TMDB;

namespace Shoko.Server.Mappings;

public class TMDB_AlternateOrdering_SeasonMap : ClassMap<TMDB_AlternateOrdering_Season>
{
    public TMDB_AlternateOrdering_SeasonMap()
    {
        Table("TMDB_AlternateOrdering_Season");

        Not.LazyLoad();
        Id(x => x.TMDB_AlternateOrdering_SeasonID);

        Map(x => x.TmdbShowID).Not.Nullable();
        Map(x => x.TmdbEpisodeGroupCollectionID).Not.Nullable();
        Map(x => x.TmdbEpisodeGroupID).Not.Nullable();
        Map(x => x.EnglishTitle).Not.Nullable();
        Map(x => x.EpisodeCount).Not.Nullable();
        Map(x => x.HiddenEpisodeCount).Not.Nullable();
        Map(x => x.SeasonNumber).Not.Nullable();
        Map(x => x.IsLocked).Not.Nullable();
        Map(x => x.CreatedAt).Not.Nullable();
        Map(x => x.LastUpdatedAt).Not.Nullable();
    }
}

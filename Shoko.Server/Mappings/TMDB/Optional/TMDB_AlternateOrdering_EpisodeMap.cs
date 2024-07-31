using FluentNHibernate.Mapping;
using Shoko.Server.Models.TMDB;

namespace Shoko.Server.Mappings;

public class TMDB_AlternateOrdering_EpisodeMap : ClassMap<TMDB_AlternateOrdering_Episode>
{
    public TMDB_AlternateOrdering_EpisodeMap()
    {
        Table("TMDB_AlternateOrdering_Episode");

        Not.LazyLoad();
        Id(x => x.TMDB_AlternateOrdering_EpisodeID);

        Map(x => x.TmdbShowID).Not.Nullable();
        Map(x => x.TmdbEpisodeGroupCollectionID).Not.Nullable();
        Map(x => x.TmdbEpisodeGroupID).Not.Nullable();
        Map(x => x.TmdbEpisodeID).Not.Nullable();
        Map(x => x.SeasonNumber).Not.Nullable();
        Map(x => x.EpisodeNumber).Not.Nullable();
        Map(x => x.CreatedAt).Not.Nullable();
        Map(x => x.LastUpdatedAt).Not.Nullable();
    }
}

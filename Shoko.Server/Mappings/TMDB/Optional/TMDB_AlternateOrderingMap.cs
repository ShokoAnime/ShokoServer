using FluentNHibernate.Mapping;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Providers.TMDB;

namespace Shoko.Server.Mappings;

public class TMDB_AlternateOrderingMap : ClassMap<TMDB_AlternateOrdering>
{
    public TMDB_AlternateOrderingMap()
    {
        Table("TMDB_AlternateOrdering");

        Not.LazyLoad();
        Id(x => x.TMDB_AlternateOrderingID);

        Map(x => x.TmdbShowID).Not.Nullable();
        Map(x => x.TmdbNetworkID);
        Map(x => x.TmdbEpisodeGroupCollectionID).Not.Nullable();
        Map(x => x.EnglishTitle).Not.Nullable();
        Map(x => x.EnglishOverview).Not.Nullable();
        Map(x => x.EpisodeCount).Not.Nullable();
        Map(x => x.SeasonCount).Not.Nullable();
        Map(x => x.Type).Not.Nullable().CustomType<AlternateOrderingType>();
        Map(x => x.CreatedAt).Not.Nullable();
        Map(x => x.LastUpdatedAt).Not.Nullable();
    }
}

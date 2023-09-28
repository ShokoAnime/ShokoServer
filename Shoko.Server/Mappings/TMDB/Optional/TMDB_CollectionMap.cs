using FluentNHibernate.Mapping;
using Shoko.Server.Models.TMDB;

namespace Shoko.Server.Mappings;

public class TMDB_CollectionMap : ClassMap<TMDB_Collection>
{
    public TMDB_CollectionMap()
    {
        Table("TMDB_Collection");

        Not.LazyLoad();
        Id(x => x.TMDB_CollectionID);

        Map(x => x.TmdbCollectionID).Not.Nullable();
        Map(x => x.EnglishTitle).Not.Nullable();
        Map(x => x.EnglishOverview).Not.Nullable();
        Map(x => x.MovieCount).Not.Nullable();
        Map(x => x.CreatedAt).Not.Nullable();
        Map(x => x.LastUpdatedAt).Not.Nullable();
    }
}

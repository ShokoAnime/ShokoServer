using FluentNHibernate.Mapping;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Models.TMDB;

namespace Shoko.Server.Mappings;

public class TMDB_SuggestionMap : ClassMap<TMDB_Suggestion>
{
    public TMDB_SuggestionMap()
    {
        Table("TMDB_Suggestion");

        Not.LazyLoad();
        Id(x => x.TMDB_SuggestionID);

        Map(x => x.TmdbEntityType).CustomType<DataEntityType>().Not.Nullable();
        Map(x => x.TmdbEntityID).Not.Nullable();
        Map(x => x.SuggestedTmdbEntityID).Not.Nullable();
        Map(x => x.Kind).CustomType<SuggestionKind>().Not.Nullable();
        Map(x => x.Ordering).Not.Nullable();
    }
}

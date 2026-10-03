using FluentNHibernate.Mapping;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.Metadata;

namespace Shoko.Server.Mappings;

public class Metadata_SuggestionMap : ClassMap<Metadata_Suggestion>
{
    public Metadata_SuggestionMap()
    {
        Table("Metadata_Suggestion");

        Not.LazyLoad();
        Id(x => x.Metadata_SuggestionID);

        Map(x => x.Source).CustomType<MetadataSourceType>().Not.Nullable();
        Map(x => x.BaseType).CustomType<MetadataEntityTypeType>().Not.Nullable();
        Map(x => x.BaseID).Not.Nullable();
        Map(x => x.SuggestedType).CustomType<MetadataEntityTypeType>().Not.Nullable();
        Map(x => x.SuggestedID).Not.Nullable();
        Map(x => x.Kind).CustomType<SuggestionKind>().Not.Nullable();
        Map(x => x.Ranking).Nullable();
        Map(x => x.ApprovalRating).Nullable();
        Map(x => x.ApprovalVotes).Nullable();
        Map(x => x.Votes).Nullable();
        Map(x => x.Score).Nullable();
        Map(x => x.Ordering).Not.Nullable();
    }
}

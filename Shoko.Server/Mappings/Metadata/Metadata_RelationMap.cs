using FluentNHibernate.Mapping;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.Metadata;

namespace Shoko.Server.Mappings;

public class Metadata_RelationMap : ClassMap<Metadata_Relation>
{
    public Metadata_RelationMap()
    {
        Table("Metadata_Relation");

        Not.LazyLoad();
        Id(x => x.Metadata_RelationID);

        Map(x => x.Source).CustomType<MetadataSourceType>().Not.Nullable();
        Map(x => x.BaseType).CustomType<MetadataEntityTypeType>().Not.Nullable();
        Map(x => x.BaseID).Not.Nullable();
        Map(x => x.RelatedType).CustomType<MetadataEntityTypeType>().Not.Nullable();
        Map(x => x.RelatedID).Not.Nullable();
        Map(x => x.RelationType).CustomType<RelationType>().Not.Nullable();
        Map(x => x.Ordering).Not.Nullable();
    }
}

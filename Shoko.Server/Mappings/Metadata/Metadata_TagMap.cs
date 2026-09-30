using FluentNHibernate.Mapping;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.Metadata;

namespace Shoko.Server.Mappings;

public class Metadata_TagMap : ClassMap<Metadata_Tag>
{
    public Metadata_TagMap()
    {
        Table("Metadata_Tag");

        Not.LazyLoad();
        Id(x => x.Metadata_TagID);

        Map(x => x.Source).CustomType<MetadataSourceType>().Not.Nullable();
        Map(x => x.ProviderID).Not.Nullable();
        Map(x => x.Name).Not.Nullable();
        Map(x => x.Description).Not.Nullable().CustomType("StringClob");
        Map(x => x.Kind).CustomType<TagKind>().Not.Nullable();
        Map(x => x.Category).Nullable();
        Map(x => x.IsSpoiler).Not.Nullable();
        Map(x => x.IsRestricted).Not.Nullable();
        Map(x => x.LastUpdatedAt).Not.Nullable();
    }
}

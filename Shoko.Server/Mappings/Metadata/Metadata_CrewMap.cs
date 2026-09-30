using FluentNHibernate.Mapping;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.Metadata;

namespace Shoko.Server.Mappings;

public class Metadata_CrewMap : ClassMap<Metadata_Crew>
{
    public Metadata_CrewMap()
    {
        Table("Metadata_Crew");

        Not.LazyLoad();
        Id(x => x.Metadata_CrewID);

        Map(x => x.Source).CustomType<MetadataSourceType>().Not.Nullable();
        Map(x => x.EntityType).CustomType<MetadataEntityTypeType>().Not.Nullable();
        Map(x => x.EntityID).Not.Nullable();
        Map(x => x.CreatorID).Not.Nullable();
        Map(x => x.Name).Not.Nullable();
        Map(x => x.RoleType).CustomType<CrewRoleType>().Not.Nullable();
        Map(x => x.LanguageCode).Nullable();
        Map(x => x.Ordering).Not.Nullable();
    }
}

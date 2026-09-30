using FluentNHibernate.Mapping;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.Metadata;

namespace Shoko.Server.Mappings;

public class Metadata_CastMap : ClassMap<Metadata_Cast>
{
    public Metadata_CastMap()
    {
        Table("Metadata_Cast");

        Not.LazyLoad();
        Id(x => x.Metadata_CastID);

        Map(x => x.Source).CustomType<MetadataSourceType>().Not.Nullable();
        Map(x => x.EntityType).CustomType<MetadataEntityTypeType>().Not.Nullable();
        Map(x => x.EntityID).Not.Nullable();
        Map(x => x.CreatorID).Nullable();
        Map(x => x.CharacterID).Nullable();
        Map(x => x.Name).Not.Nullable();
        Map(x => x.RoleType).CustomType<CastRoleType>().Not.Nullable();
        Map(x => x.LanguageCode).Nullable();
        Map(x => x.RoleNotes).Nullable().CustomType("StringClob");
        Map(x => x.DubGroup).Nullable();
        Map(x => x.Ordering).Not.Nullable();
    }
}

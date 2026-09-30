using FluentNHibernate.Mapping;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.Metadata;

namespace Shoko.Server.Mappings;

public class Metadata_CharacterMap : ClassMap<Metadata_Character>
{
    public Metadata_CharacterMap()
    {
        Table("Metadata_Character");

        Not.LazyLoad();
        Id(x => x.Metadata_CharacterID);

        Map(x => x.Source).CustomType<MetadataSourceType>().Not.Nullable();
        Map(x => x.ProviderID).Not.Nullable();
        Map(x => x.Name).Not.Nullable();
        Map(x => x.OriginalName).Nullable();
        Map(x => x.Description).Nullable().CustomType("StringClob");
        Map(x => x.Type).CustomType<CharacterType>().Not.Nullable();
        Map(x => x.Gender).CustomType<PersonGender>().Not.Nullable();
        Map(x => x.BirthDay).CustomType<FuzzyDateOnlyConverter>().Nullable();
        Map(x => x.Resources).CustomType<JsonListConverter<Resource>>().Nullable();
        Map(x => x.LastUpdatedAt).Not.Nullable();
        Map(x => x.LastOrphanedAt).Nullable();
    }
}

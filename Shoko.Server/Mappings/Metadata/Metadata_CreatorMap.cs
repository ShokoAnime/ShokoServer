using FluentNHibernate.Mapping;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Models.Metadata.Embedded;

namespace Shoko.Server.Mappings;

public class Metadata_CreatorMap : ClassMap<Metadata_Creator>
{
    public Metadata_CreatorMap()
    {
        Table("Metadata_Creator");

        Not.LazyLoad();
        Id(x => x.Metadata_CreatorID);

        Map(x => x.Source).CustomType<MetadataSourceType>().Not.Nullable();
        Map(x => x.ProviderID).Not.Nullable();
        Map(x => x.Name).Not.Nullable();
        Map(x => x.OriginalName).Nullable();
        Map(x => x.Description).Nullable().CustomType("StringClob");
        Map(x => x.Type).CustomType<CreatorType>().Not.Nullable();
        Map(x => x.Gender).CustomType<PersonGender>().Not.Nullable();
        Map(x => x.BirthDay).CustomType<FuzzyDateOnlyConverter>().Nullable();
        Map(x => x.DeathDay).CustomType<FuzzyDateOnlyConverter>().Nullable();
        Map(x => x.Resources).CustomType<JsonListConverter<Resource>>().Nullable();
        Map(x => x.CreatedAt).Not.Nullable();
        Map(x => x.LastUpdatedAt).Nullable();
        Map(x => x.LastOrphanedAt).Nullable();
        Map(x => x.LastRefreshedAt).Nullable();
        Map(x => x.IsRestricted).Not.Nullable();
        Map(x => x.ExtraData).CustomType<JsonObjectConverter<Metadata_CreatorExtra>>().Nullable();
    }
}

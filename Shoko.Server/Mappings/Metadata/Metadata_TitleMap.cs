using FluentNHibernate.Mapping;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.Metadata;

namespace Shoko.Server.Mappings;

public class Metadata_TitleMap : ClassMap<Metadata_Title>
{
    public Metadata_TitleMap()
    {
        Table("Metadata_Title");

        Not.LazyLoad();
        Id(x => x.Metadata_TitleID);

        Map(x => x.EntitySource).CustomType<MetadataSourceType>().Not.Nullable();
        Map(x => x.EntityType).CustomType<MetadataEntityTypeType>().Not.Nullable();
        Map(x => x.EntityID).Not.Nullable();
        Map(x => x.Source).CustomType<MetadataSourceType>().Not.Nullable();
        Map(x => x.Language).CustomType<TitleLanguageConverter>().Not.Nullable();
        Map(x => x.LanguageCode).Not.Nullable();
        Map(x => x.CountryCode).Nullable();
        Map(x => x.ScriptCode).Nullable();
        Map(x => x.TitleType).CustomType<TitleType>().Not.Nullable();
        Map(x => x.Value).Not.Nullable().CustomType("StringClob");
        Map(x => x.IsEnabled).Not.Nullable();
        Map(x => x.Preference).CustomType<TextPreference>().Not.Nullable();
        Map(x => x.Ordering).Not.Nullable();
        Map(x => x.ReferenceID).Nullable();
    }
}

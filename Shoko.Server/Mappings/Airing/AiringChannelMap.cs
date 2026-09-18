using FluentNHibernate.Mapping;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.Airing;

namespace Shoko.Server.Mappings.Airing;

public class AiringChannelMap : ClassMap<AiringChannel>
{
    public AiringChannelMap()
    {
        Table("AiringChannel");

        Not.LazyLoad();
        Id(x => x.AiringChannelID);

        Map(x => x.ChannelID).Not.Nullable().Unique();
        Map(x => x.Name).Not.Nullable();
        Map(x => x.NormalizedName).Not.Nullable();
        Map(x => x.Type).CustomType<AiringChannelType>().Not.Nullable();
        Map(x => x.Aliases).CustomType<JsonListConverter<string>>().Not.Nullable();
        Map(x => x.CreatedAt).Not.Nullable();
    }
}

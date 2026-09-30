using FluentNHibernate.Mapping;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Providers.AniDB;

namespace Shoko.Server.Mappings;

public class AniDB_ResourceMap : ClassMap<AniDB_Resource>
{
    public AniDB_ResourceMap()
    {
        Table("AniDB_Resource");
        Not.LazyLoad();
        Id(x => x.AniDB_ResourceID);

        Map(x => x.AnimeID).Not.Nullable();
        Map(x => x.EpisodeID).Nullable();
        Map(x => x.ResourceType).CustomType<ResourceLinkType>().Not.Nullable();
        Map(x => x.Ordering).Not.Nullable();
        Map(x => x.Identifiers).CustomType<JsonListConverter<string>>().Not.Nullable();
        Map(x => x.Urls).CustomType<JsonListConverter<string>>().Not.Nullable();
    }
}

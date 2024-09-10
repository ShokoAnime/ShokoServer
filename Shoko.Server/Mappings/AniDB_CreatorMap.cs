using FluentNHibernate.Mapping;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Providers.AniDB;

namespace Shoko.Server.Mappings;

public class AniDB_CreatorMap : ClassMap<AniDB_Creator>
{
    public AniDB_CreatorMap()
    {
        Not.LazyLoad();
        Id(x => x.AniDB_CreatorID);

        Map(x => x.CreatorID).Not.Nullable();
        Map(x => x.Name).Not.Nullable();
        Map(x => x.OriginalName);
        Map(x => x.Type).CustomType<CreatorType>().Not.Nullable();
        Map(x => x.ImagePath);
        Map(x => x.EnglishHomepageUrl);
        Map(x => x.JapaneseHomepageUrl);
        Map(x => x.EnglishWikiUrl);
        Map(x => x.JapaneseWikiUrl);
        Map(x => x.LastUpdatedAt).Not.Nullable();
    }
}

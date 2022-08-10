using FluentNHibernate.Mapping;
using Shoko.Server.Models;
using Shoko.Server.Databases.TypeConverters;

namespace Shoko.Server.Mappings
{
    public class AniDB_Anime_TitleMap : ClassMap<SVR_AniDB_Anime_Title>
    {
        public AniDB_Anime_TitleMap()
        {
            Table("AniDB_Anime_Title");
            Not.LazyLoad();
            Id(x => x.AniDB_Anime_TitleID);

            Map(x => x.AnimeID).Not.Nullable();
            Map(x => x.Language).CustomType<TitleLanguageConverter>().Not.Nullable();
            Map(x => x.Title).Not.Nullable();
            Map(x => x.TitleType).CustomType<TitleTypeConverter>().Not.Nullable();
        }
    }
}
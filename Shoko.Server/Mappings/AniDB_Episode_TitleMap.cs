using FluentNHibernate.Mapping;
using Shoko.Server.Databases.TypeConverters;
using Shoko.Server.Models;

namespace Shoko.Server.Mappings
{
    public class AniDB_Episode_TitleMap : ClassMap<SVR_AniDB_Episode_Title>
    {
        public AniDB_Episode_TitleMap()
        {
            Table("AniDB_Episode_Title");
            Not.LazyLoad();
            Id(x => x.AniDB_Episode_TitleID);

            Map(x => x.AniDB_EpisodeID).Not.Nullable();
            Map(x => x.Language).CustomType<TitleLanguageConverter>().Not.Nullable();
            Map(x => x.Title).Not.Nullable();
        }
    }
}

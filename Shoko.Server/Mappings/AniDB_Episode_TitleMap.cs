using FluentNHibernate.Mapping;
using Shoko.Models.Server;
using Shoko.Server.Models;

namespace Shoko.Server.Mappings
{
    public class AniDB_Episode_TitleMap : ClassMap<AniDB_Episode_Title>
    {
        public AniDB_Episode_TitleMap()
        {
            Table("AniDB_Episode_Title");
            Not.LazyLoad();
            Id(x => x.AniDB_Episode_TitleID);

            Map(x => x.AniDB_EpisodeID).Not.Nullable();
            Map(x => x.Language).Not.Nullable();
            Map(x => x.Title).Not.Nullable();
        }
    }
}

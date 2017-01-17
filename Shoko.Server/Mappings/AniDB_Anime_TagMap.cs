using FluentNHibernate.Mapping;
using Shoko.Models.Server;
using Shoko.Server.Models;

namespace Shoko.Server.Mappings
{
    public class AniDB_Anime_TagMap : ClassMap<AniDB_Anime_Tag>
    {
        public AniDB_Anime_TagMap()
        {
            Table("AniDB_Anime_Tag");
            Not.LazyLoad();
            Id(x => x.AniDB_Anime_TagID);

            Map(x => x.AnimeID).Not.Nullable();
            Map(x => x.Approval).Not.Nullable();
            Map(x => x.Weight).Not.Nullable();
            Map(x => x.TagID).Not.Nullable();
        }
    }
}
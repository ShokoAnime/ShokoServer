using FluentNHibernate.Mapping;
using JMMServer.Entities;
using Shoko.Models.Server;

namespace JMMServer.Mappings
{
    public class AniDB_Anime_TagMap : ClassMap<SVR_AniDB_Anime_Tag>
    {
        public AniDB_Anime_TagMap()
        {
            Not.LazyLoad();
            Id(x => x.AniDB_Anime_TagID);

            Map(x => x.AnimeID).Not.Nullable();
            Map(x => x.Approval).Not.Nullable();
            Map(x => x.Weight).Not.Nullable();
            Map(x => x.TagID).Not.Nullable();
        }
    }
}
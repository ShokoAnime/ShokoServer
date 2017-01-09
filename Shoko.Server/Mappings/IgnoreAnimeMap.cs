using FluentNHibernate.Mapping;
using Shoko.Models.Server;
using Shoko.Server.Entities;

namespace Shoko.Server.Mappings
{
    public class IgnoreAnimeMap : ClassMap<IgnoreAnime>
    {
        public IgnoreAnimeMap()
        {
            Not.LazyLoad();
            Id(x => x.IgnoreAnimeID);

            Map(x => x.AnimeID).Not.Nullable();
            Map(x => x.JMMUserID).Not.Nullable();
            Map(x => x.IgnoreType).Not.Nullable();
        }
    }
}
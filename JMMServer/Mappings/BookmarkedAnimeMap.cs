using FluentNHibernate.Mapping;
using JMMServer.Entities;
using Shoko.Models.Server;

namespace JMMServer.Mappings
{
    public class BookmarkedAnimeMap : ClassMap<SVR_BookmarkedAnime>
    {
        public BookmarkedAnimeMap()
        {
            Not.LazyLoad();
            Id(x => x.BookmarkedAnimeID);

            Map(x => x.AnimeID).Not.Nullable();
            Map(x => x.Priority).Not.Nullable();
            Map(x => x.Notes);
            Map(x => x.Downloading).Not.Nullable();
        }
    }
}
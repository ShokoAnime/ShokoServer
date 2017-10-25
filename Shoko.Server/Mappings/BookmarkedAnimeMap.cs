using FluentNHibernate.Mapping;
using Shoko.Models.Server;
using Shoko.Server.Models;

namespace Shoko.Server.Mappings
{
    public class BookmarkedAnimeMap : ClassMap<BookmarkedAnime>
    {
        public BookmarkedAnimeMap()
        {
            Table("BookmarkedAnime");
            Not.LazyLoad();
            Id(x => x.BookmarkedAnimeID);

            Map(x => x.AnimeID).Not.Nullable();
            Map(x => x.Priority).Not.Nullable();
            Map(x => x.Notes);
            Map(x => x.Downloading).Not.Nullable();
        }
    }
}
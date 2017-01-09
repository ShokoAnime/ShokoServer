using FluentNHibernate.Mapping;
using Shoko.Models.Server;
using Shoko.Server.Entities;

namespace Shoko.Server.Mappings
{
    public class MovieDB_MovieMap : ClassMap<MovieDB_Movie>
    {
        public MovieDB_MovieMap()
        {
            Not.LazyLoad();
            Id(x => x.MovieDB_MovieID);

            Map(x => x.MovieId).Not.Nullable();
            Map(x => x.MovieName);
            Map(x => x.OriginalName);
            Map(x => x.Overview).CustomType("StringClob");
        }
    }
}
using FluentNHibernate.Mapping;
using Shoko.Models.Server;

namespace Shoko.Server.Mappings
{
    public class MovieDB_MovieMap : ClassMap<MovieDB_Movie>
    {
        public MovieDB_MovieMap()
        {
            Not.LazyLoad();
            Id(x => x.MovieId).Not.Nullable();
            Map(x => x.MovieName);
            Map(x => x.OriginalName);
            Map(x => x.Overview).CustomType("StringClob");
            Map(x => x.Rating).Not.Nullable();
            Map(x => x.MD5).Nullable();
            Map(x => x.Blob).Nullable().CustomType("BinaryBlob");

        }
    }
}
using FluentNHibernate.Mapping;
using Shoko.Models.Server;

namespace Shoko.Server.Mappings
{
    public class MovieDB_PosterMap : ClassMap<MovieDB_Poster>
    {
        public MovieDB_PosterMap()
        {
            Not.LazyLoad();
            Id(x => x.MovieDB_PosterID);

            Map(x => x.Enabled).Not.Nullable();
            Map(x => x.ImageHeight).Not.Nullable();
            Map(x => x.ImageID).Not.Nullable();
            Map(x => x.ImageSize);
            Map(x => x.ImageType);
            Map(x => x.ImageWidth).Not.Nullable();
            Map(x => x.MovieId).Not.Nullable();
            Map(x => x.URL);
        }
    }
}
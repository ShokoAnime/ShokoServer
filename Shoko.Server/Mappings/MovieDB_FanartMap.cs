using FluentNHibernate.Mapping;
using Shoko.Models.Server;
using Shoko.Server.Models;

namespace Shoko.Server.Mappings
{
    public class MovieDB_FanartMap : ClassMap<MovieDB_Fanart>
    {
        public MovieDB_FanartMap()
        {
            Not.LazyLoad();
            Id(x => x.MovieDB_FanartID);

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
using FluentNHibernate.Mapping;
using Shoko.Models.Server;
using Shoko.Server.Entities;

namespace Shoko.Server.Mappings
{
    public class Trakt_ImagePosterMap : ClassMap<Trakt_ImagePoster>
    {
        public Trakt_ImagePosterMap()
        {
            Not.LazyLoad();
            Id(x => x.Trakt_ImagePosterID);

            Map(x => x.Enabled).Not.Nullable();
            Map(x => x.ImageURL);
            Map(x => x.Season).Not.Nullable();
            Map(x => x.Trakt_ShowID).Not.Nullable();
        }
    }
}
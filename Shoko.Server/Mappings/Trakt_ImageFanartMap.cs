using FluentNHibernate.Mapping;
using Shoko.Models.Server;
using Shoko.Server.Models;

namespace Shoko.Server.Mappings
{
    public class Trakt_ImageFanartMap : ClassMap<Trakt_ImageFanart>
    {
        public Trakt_ImageFanartMap()
        {
            Not.LazyLoad();
            Id(x => x.Trakt_ImageFanartID);

            Map(x => x.Enabled).Not.Nullable();
            Map(x => x.ImageURL);
            Map(x => x.Season).Not.Nullable();
            Map(x => x.Trakt_ShowID).Not.Nullable();
        }
    }
}
using FluentNHibernate.Mapping;
using Shoko.Models.Server;
using Shoko.Server.Models;

namespace Shoko.Server.Mappings
{
    public class TvDB_SeriesMap : ClassMap<TvDB_Series>
    {
        public TvDB_SeriesMap()
        {
            Not.LazyLoad();
            Id(x => x.TvDB_SeriesID);

            Map(x => x.SeriesID).Not.Nullable();
            Map(x => x.Banner);
            Map(x => x.Fanart);
            Map(x => x.Lastupdated);
            Map(x => x.Overview);
            Map(x => x.Poster);
            Map(x => x.SeriesName);
            Map(x => x.Status);
            Map(x => x.Rating);
        }
    }
}
using FluentNHibernate.Mapping;
using Shoko.Models.Server;

namespace Shoko.Server.Mappings
{
    public class MovieDBSeriesMap : ClassMap<MovieDB_Series>
    {
        public MovieDBSeriesMap()
        {
            Not.LazyLoad();
            Id(x => x.SeriesID).Not.Nullable();
            Map(x => x.SeriesName);
            Map(x => x.OriginalName);
            Map(x => x.Overview).CustomType("StringClob");
            Map(x => x.Rating).Not.Nullable();
            Map(x => x.MD5).Nullable();
            Map(x => x.Blob).Nullable().CustomType("BinaryBlob");

        }
    }
}

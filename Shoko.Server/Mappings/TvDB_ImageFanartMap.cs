using FluentNHibernate.Mapping;
using Shoko.Models.Server;

namespace Shoko.Server.Mappings
{
    public class TvDB_ImageFanartMap : ClassMap<TvDB_ImageFanart>
    {
        public TvDB_ImageFanartMap()
        {
            Not.LazyLoad();
            Id(x => x.TvDB_ImageFanartID);

            Map(x => x.BannerPath);
            Map(x => x.BannerType);
            Map(x => x.BannerType2);
            Map(x => x.Chosen).Not.Nullable();
            Map(x => x.Colors);
            Map(x => x.Enabled).Not.Nullable();
            Map(x => x.Id).Not.Nullable();
            Map(x => x.Language);
            Map(x => x.SeriesID).Not.Nullable();
            Map(x => x.ThumbnailPath);
            Map(x => x.VignettePath);
        }
    }
}
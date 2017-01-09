using FluentNHibernate.Mapping;
using Shoko.Models.Server;
using Shoko.Server.Entities;

namespace Shoko.Server.Mappings
{
    public class TvDB_ImagePosterMap : ClassMap<TvDB_ImagePoster>
    {
        public TvDB_ImagePosterMap()
        {
            Not.LazyLoad();
            Id(x => x.TvDB_ImagePosterID);

            Map(x => x.BannerPath);
            Map(x => x.BannerType);
            Map(x => x.BannerType2);
            Map(x => x.Enabled).Not.Nullable();
            Map(x => x.Id).Not.Nullable();
            Map(x => x.Language);
            Map(x => x.SeriesID).Not.Nullable();
            Map(x => x.SeasonNumber);
        }
    }
}
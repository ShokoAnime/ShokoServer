using FluentNHibernate.Mapping;
using Shoko.Models.Server;
using Shoko.Server.Entities;

namespace Shoko.Server.Mappings
{
    public class TvDB_EpisodeMap : ClassMap<TvDB_Episode>
    {
        public TvDB_EpisodeMap()
        {
            Not.LazyLoad();
            Id(x => x.TvDB_EpisodeID);

            Map(x => x.AbsoluteNumber);
            Map(x => x.EpImgFlag).Not.Nullable();
            Map(x => x.EpisodeName);
            Map(x => x.EpisodeNumber).Not.Nullable();
            Map(x => x.Filename);
            Map(x => x.Id).Not.Nullable();
            Map(x => x.Overview).CustomType("StringClob");
            Map(x => x.SeasonID).Not.Nullable();
            Map(x => x.SeasonNumber).Not.Nullable();
            Map(x => x.SeriesID).Not.Nullable();

            Map(x => x.AirsAfterSeason);
            Map(x => x.AirsBeforeEpisode);
            Map(x => x.AirsBeforeSeason);
        }
    }
}
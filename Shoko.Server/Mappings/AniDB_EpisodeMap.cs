using FluentNHibernate.Mapping;
using Shoko.Models.Server;

namespace Shoko.Server.Mappings
{
    public class AniDB_EpisodeMap : ClassMap<AniDB_Episode>
    {
        public AniDB_EpisodeMap()
        {
            Table("AniDB_Episode");
            Not.LazyLoad();
            Id(x => x.AniDB_EpisodeID);

            Map(x => x.AirDate).Not.Nullable();
            Map(x => x.AnimeID).Not.Nullable();
            Map(x => x.DateTimeUpdated).Not.Nullable();
            Map(x => x.Description).Not.Nullable().CustomType("StringClob");
            Map(x => x.EpisodeID).Not.Nullable();
            Map(x => x.EpisodeNumber).Not.Nullable();
            Map(x => x.EpisodeType).Not.Nullable();
            Map(x => x.LengthSeconds).Not.Nullable();
            Map(x => x.Rating).Not.Nullable();
            Map(x => x.Votes).Not.Nullable();
        }
    }
}

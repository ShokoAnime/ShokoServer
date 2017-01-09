using FluentNHibernate.Mapping;
using Shoko.Models.Server;
using Shoko.Server.Entities;

namespace Shoko.Server.Mappings
{
    public class AnimeEpisodeMap : ClassMap<SVR_AnimeEpisode>
    {
        public AnimeEpisodeMap()
        {
            Not.LazyLoad();
            Id(x => x.AnimeEpisodeID);

            Map(x => x.AniDB_EpisodeID).Not.Nullable();
            Map(x => x.AnimeSeriesID).Not.Nullable();
            Map(x => x.DateTimeCreated).Not.Nullable();
            Map(x => x.DateTimeUpdated).Not.Nullable();
            Map(x => x.PlexContractVersion).Not.Nullable();
            Map(x => x.PlexContractBlob).Nullable().CustomType("BinaryBlob");
            Map(x => x.PlexContractSize).Not.Nullable();
        }
    }
}
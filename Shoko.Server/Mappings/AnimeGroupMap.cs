using FluentNHibernate.Mapping;
using Shoko.Server.Models;

namespace Shoko.Server.Mappings
{
    public class AnimeGroupMap : ClassMap<SVR_AnimeGroup>
    {
        public AnimeGroupMap()
        {
            Table("AnimeGroup");
            Not.LazyLoad();
            Id(x => x.AnimeGroupID);
            Map(x => x.AnimeGroupParentID);
            Map(x => x.DefaultAnimeSeriesID);
            Map(x => x.MainAniDBAnimeID);
            Map(x => x.DateTimeCreated).Not.Nullable();
            Map(x => x.DateTimeUpdated).Not.Nullable();
            Map(x => x.Description).CustomType("StringClob").CustomSqlType("nvarchar(max)");
            Map(x => x.GroupName);
            Map(x => x.IsManuallyNamed).Not.Nullable();
            Map(x => x.OverrideDescription).Not.Nullable();
            Map(x => x.SortName);
            Map(x => x.EpisodeAddedDate);
            Map(x => x.LatestEpisodeAirDate);
            Map(x => x.MissingEpisodeCount).Not.Nullable();
            Map(x => x.MissingEpisodeCountGroups).Not.Nullable();
            Map(x => x.ContractVersion).Not.Nullable();
            Map(x => x.ContractBlob).Nullable().CustomType("BinaryBlob");
            Map(x => x.ContractSize).Not.Nullable();
        }
    }
}
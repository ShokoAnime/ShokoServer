using FluentNHibernate.Mapping;
using Shoko.Models.Server;
using Shoko.Server.Entities;

namespace Shoko.Server.Mappings
{
    public class AniDB_AnimeMap : ClassMap<SVR_AniDB_Anime>
    {
        public AniDB_AnimeMap()
        {
            Not.LazyLoad();
            Id(x => x.AniDB_AnimeID);

            Map(x => x.AirDate);
            Map(x => x.AllCinemaID);
            Map(x => x.AllTitles);
            Map(x => x.AllTags);
            Map(x => x.AnimeNfo);
            Map(x => x.AnimeID).Not.Nullable();
            Map(x => x.AnimePlanetID);
            Map(x => x.AnimeType).Not.Nullable();
            Map(x => x.ANNID);
            Map(x => x.AvgReviewRating).Not.Nullable();
            Map(x => x.AwardList).Not.Nullable();
            Map(x => x.BeginYear).Not.Nullable();
            Map(x => x.DateTimeDescUpdated).Not.Nullable();
            Map(x => x.DateTimeUpdated).Not.Nullable();
            Map(x => x.Description).Not.Nullable();
            Map(x => x.EndDate);
            Map(x => x.EndYear).Not.Nullable();
            Map(x => x.EpisodeCount).Not.Nullable();
            Map(x => x.EpisodeCountNormal).Not.Nullable();
            Map(x => x.EpisodeCountSpecial).Not.Nullable();
            Map(x => x.ImageEnabled).Not.Nullable();
            Map(x => x.LatestEpisodeNumber);
            Map(x => x.MainTitle).Not.Nullable();
            Map(x => x.Picname);
            Map(x => x.Rating).Not.Nullable();
            Map(x => x.Restricted).Not.Nullable();
            Map(x => x.ReviewCount).Not.Nullable();
            Map(x => x.TempRating).Not.Nullable();
            Map(x => x.TempVoteCount).Not.Nullable();
            Map(x => x.URL);
            Map(x => x.VoteCount).Not.Nullable();
            Map(x => x.DisableExternalLinksFlag).Not.Nullable();
            Map(x => x.ContractVersion).Not.Nullable();
            Map(x => x.ContractBlob).Nullable().CustomType("BinaryBlob");
            Map(x => x.ContractSize).Not.Nullable();
        }
    }
}
﻿using FluentNHibernate.Mapping;
using Shoko.Server.Models;

namespace Shoko.Server.Mappings;

public class AniDB_AnimeMap : ClassMap<SVR_AniDB_Anime>
{
    public AniDB_AnimeMap()
    {
        Table("AniDB_Anime");
        Not.LazyLoad();
        Id(x => x.AniDB_AnimeID);

        Map(x => x.AirDate);
        Map(x => x.AllCinemaID);
        Map(x => x.AllTitles);
        Map(x => x.AllTags);
        Map(x => x.AnimeID).Not.Nullable();
        Map(x => x.AnimeType).Not.Nullable();
        Map(x => x.ANNID);
        Map(x => x.AnisonID);
        Map(x => x.SyoboiID);
        Map(x => x.VNDBID);
        Map(x => x.BangumiID);
        Map(x => x.LainID);
        Map(x => x.Site_EN);
        Map(x => x.Site_JP);
        Map(x => x.Wikipedia_ID);
        Map(x => x.WikipediaJP_ID);
        Map(x => x.CrunchyrollID);
        Map(x => x.FunimationID);
        Map(x => x.HiDiveID);
        Map(x => x.AvgReviewRating).Not.Nullable();
        Map(x => x.BeginYear).Not.Nullable();
        Map(x => x.DateTimeDescUpdated).Not.Nullable();
#pragma warning disable CS0618
        Map(x => x.DateTimeUpdated).Not.Nullable();
#pragma warning restore CS0618
        Map(x => x.Description).CustomType("StringClob").Not.Nullable();
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
    }
}

﻿using FluentNHibernate.Mapping;
using Shoko.Models.Server;
using Shoko.Server.Models;

namespace Shoko.Server.Mappings
{
    public class AnimeSeriesMap : ClassMap<SVR_AnimeSeries>
    {
        public AnimeSeriesMap()
        {
            Table("AnimeSeries");
            Not.LazyLoad();
            Id(x => x.AnimeSeriesID);

            Map(x => x.AniDB_ID).Not.Nullable();
            Map(x => x.AnimeGroupID).Not.Nullable();
            Map(x => x.DateTimeCreated).Not.Nullable();
            Map(x => x.DateTimeUpdated).Not.Nullable();
            Map(x => x.DefaultAudioLanguage);
            Map(x => x.DefaultSubtitleLanguage);
            Map(x => x.LatestLocalEpisodeNumber).Not.Nullable();
            Map(x => x.EpisodeAddedDate);
            Map(x => x.LatestEpisodeAirDate);
            Map(x => x.MissingEpisodeCount).Not.Nullable();
            Map(x => x.MissingEpisodeCountGroups).Not.Nullable();
            Map(x => x.SeriesNameOverride);
            Map(x => x.DefaultFolder);
            Map(x => x.ContractVersion).Not.Nullable();
            Map(x => x.ContractBlob).Nullable().CustomType("BinaryBlob");
            Map(x => x.ContractSize).Not.Nullable();
            Map(x => x.AirsOn);
            Map(x => x.UpdatedAt).Not.Nullable();
        }
    }
}
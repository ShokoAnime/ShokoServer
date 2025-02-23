using System;

namespace Shoko.Models.Server
{
    public class CrossRef_AniDB_TvDBV2 : ICloneable
    {
        public int CrossRef_AniDB_TvDBV2ID { get; set; }
        public int AnimeID { get; set; }
        public int AniDBStartEpisodeType { get; set; }
        public int AniDBStartEpisodeNumber { get; set; }
        public int TvDBID { get; set; }
        public int TvDBSeasonNumber { get; set; }
        public int TvDBStartEpisodeNumber { get; set; }
        public string TvDBTitle { get; set; }

        public int CrossRefSource { get; set; }
        public bool IsAdditive { get; set; }

        public object Clone()
        {
            return new CrossRef_AniDB_TvDBV2
            {
                CrossRef_AniDB_TvDBV2ID = CrossRef_AniDB_TvDBV2ID,
                AnimeID = AnimeID,
                AniDBStartEpisodeType = AniDBStartEpisodeType,
                AniDBStartEpisodeNumber = AniDBStartEpisodeNumber,
                TvDBID = TvDBID,
                TvDBSeasonNumber = TvDBSeasonNumber,
                TvDBStartEpisodeNumber = TvDBStartEpisodeNumber,
                TvDBTitle = TvDBTitle,
                CrossRefSource = CrossRefSource,
                IsAdditive = IsAdditive
            };
        }
    }
}

using System;

namespace JMMModels.Childs
{
    public class AniDB_Anime_TvDB : ProviderExtendedCrossRef
    {
        public int TvDBSeriesId { get; set; }
        public string Overview { get; set; }
        public string Banner { get; set; }
        public string Fanart { get; set; }
        public string Poster { get; set; } 
        public TvDB_StatusTypes Status { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}

using System;

namespace Shoko.Models.Server
{
    public class AnimeGroup 
    {
        #region Server DB Columns

        public int AnimeGroupID { get; set; }
        public int? AnimeGroupParentID { get; set; }
        public string GroupName { get; set; }
        public string Description { get; set; }
        public int IsManuallyNamed { get; set; }
        public DateTime DateTimeUpdated { get; set; }
        public DateTime DateTimeCreated { get; set; }
        public string SortName { get; set; }
        public DateTime? EpisodeAddedDate { get; set; }
        public DateTime? LatestEpisodeAirDate { get; set; }
        public int MissingEpisodeCount { get; set; }
        public int MissingEpisodeCountGroups { get; set; }
        public int OverrideDescription { get; set; }
        public int? DefaultAnimeSeriesID { get; set; }
#endregion


    }
}
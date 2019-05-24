using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Shoko.Database.Models
{
    public class AnimeGroup
    {
        [Key, Column("AnimeGroupID")] public int Id { get; set; }
        [Column("AnimeGroupParentID"), ForeignKey(nameof(Parent))] public int? ParentId { get; set; }
        [Column("GroupName")] public string Name { get; set; }
        public string Description { get; set; }
        public bool IsManuallyNamed { get; set; }
        public DateTime DateTimeUpdated { get; set; }
        public DateTime DateTimeCreated { get; set; }
        public string SortName { get; set; }
        public int MissingEpisodeCount { get; set; }
        public int MissingEpisodeCountGroups { get; set; }
        public bool OverrideDescription { get; set; }
        public DateTime? EpisodeAddedDate { get; set; }
        [ForeignKey(nameof(DefaultAnimeSeries))] public int? DefaultAnimeSeriesId { get; set; }
        public DateTime? LatestEpisodeAirDate { get; set; }


        public int ContractVersion { get; set; }
        public byte[] ContractBlob { get; set; } = null;
        public int ContractSize { get; set; }

        public virtual AnimeGroup Parent { get; set; }
        public virtual AnimeSeries DefaultAnimeSeries { get; set; }
    }
}
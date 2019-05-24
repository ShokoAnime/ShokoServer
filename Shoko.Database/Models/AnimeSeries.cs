using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Shoko.Database.Models
{
    public class AnimeSeries
    {
        [Key, Column("AnimeSeriesID")] public int Id { get; set; }
        [ForeignKey(nameof(Group))] public int AnimeGroupId { get; set; }
        [Column("AniDB_ID")] public int AniDbId { get; set; }
        public DateTime DateTimeUpdated { get; set; }
        public DateTime DateTimeCreated { get; set; }
        [MaxLength(50)] public string DefaultAudioLanguage { get; set; }
        [MaxLength(50)] public string DefaultSubtitleLanguage { get; set; }
        public int MissingEpisodeCount { get; set; }
        public int MissingEpisodeCountGroups { get; set; }
        public int LatestLocalEpisodeNumber { get; set; }
        public DateTime EpisodeAddedDate { get; set; }
        public string SeriesNameOverride { get; set; }
        public string DefaultFolder { get; set; }
        public DateTime LatestEpisodeAirDate { get; set; }
        public string AirsOn { get; set; }

        public int ContractVersion { get; set; }
        public byte[] ContractBlob { get; set; } = null;
        public int ContractSize { get; set; }

        public virtual AnimeGroup Group { get; set; }
    }
}
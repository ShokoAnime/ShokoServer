using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models
{
    public class AnimeEpisode
    {
        [Key, Column("AnimeEpisodeId")] public int Id { get; set; }
        [ForeignKey(nameof(AnimeSeries))] public int AnimeSeriesId { get; set; }
        public int AniDB_EpisodeID { get; set; } //FK?

        [Required] public DateTime DateTimeUpdated { get; set; }
        [Required] public DateTime DateTimeCreated { get; set; }

        public int PlexContractVersion { get; set; } = 0;
        public byte[] PlexContractBlob { get; set; } = null;
        public int PlexContractSize { get; set; } = 0;


        public virtual AnimeSeries AnimeSeries { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models.AniDb
{
    [Table("AniDB_File")]
    public class File
    {
        [Key, Column("AniDB_FileID")] public int Id { get; set; }
        public int FileId { get; set; }
        [Required, MaxLength(50)] public string Hash { get; set; }
        public int AnimeID { get; set; }
        public int GroupID { get; set; }
        [Column("File_Source"), MaxLength(200)] public string Source { get; set; }
        [Column("File_AudioCodec"), MaxLength(200)] public string AudioCodec { get; set; }
        [Column("File_VideoCodec"), MaxLength(200)] public string VideoCodec { get; set; }
        [Column("File_VideoResolution"), MaxLength(200)] public string VideoResolution { get; set; }
        [Column("File_FileExtension"), MaxLength(200)] public string Extension { get; set; }
        [Column("File_LengthSeconds")] public int LengthSeconds { get; set; }
        [Column("File_Description")] public string Description { get; set; }
        [Column("File_ReleaseDate")] public int ReleaseDate { get; set; }

        [Column("Anime_GroupName")] public string GroupName { get; set; }
        [Column("Anime_GroupNameShort")] public string GroupNameShort { get; set; }
        [Column("Episode_Rating")] public int Rating { get; set; }
        [Column("Episode_Votes")] public int Votes { get; set; }
        [Required] public DateTime DateTimeUpdated { get; set; }
        public bool IsWatched { get; set; }
        public DateTime WatchedDate { get; set; }

        [Required, MaxLength(200)] public string CRC { get; set; }
        [Required, MaxLength(200)] public string MD5 { get; set; }
        [Required, MaxLength(200)] public string SHA1 { get; set; }
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public int FileVersion { get; set; }
        public bool IsCensored { get; set; }
        public bool IsDeprecated { get; set; }
        public int InternalVersion { get; set; }
        public bool IsChaptered { get; set; }
    }
}
 
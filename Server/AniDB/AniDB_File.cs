using System;

namespace Shoko.Models.Server
{
    public class AniDB_File
    {
        public int AniDB_FileID { get; set; }
        public int FileID { get; set; }
        public string Hash { get; set; }
        //TODO SHOULD BE REMOVED AniDB_File might belongs to one Anime, but it may have multiple episodes that belongs to different anime, or should be used as references of the first anime
        [Obsolete("AniDB_Files are many to many mapped to Episodes to Anime. This should only be used if absolutely necessary")]
        public int AnimeID { get; set; }
        public int GroupID { get; set; }
        public string File_Source { get; set; }
        public string File_Description { get; set; }
        public int File_ReleaseDate { get; set; }
        public DateTime DateTimeUpdated { get; set; }
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public int FileVersion { get; set; }
        public bool? IsCensored { get; set; }
        public bool IsDeprecated { get; set; }
        public int InternalVersion { get; set; }
        public bool IsChaptered { get; set; }
    }
}
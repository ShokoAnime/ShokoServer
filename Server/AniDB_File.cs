using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;


namespace Shoko.Models.Server
{
    public class AniDB_File
    {
        #region DB columns

        public int AniDB_FileID { get; private set; }
        public int FileID { get; set; }
        public string Hash { get; set; }
        //TODO SHOULD BE REMOVED AniDB_File might belongs to one Anime, but it may have multiple episodes that belongs to different anime, or should be used as references of the first anime
        public int AnimeID { get; set; }
        public int GroupID { get; set; }
        public string File_Source { get; set; }
        public string File_AudioCodec { get; set; }
        public string File_VideoCodec { get; set; }
        public string File_VideoResolution { get; set; }
        public string File_FileExtension { get; set; }
        public int File_LengthSeconds { get; set; }
        public string File_Description { get; set; }
        public int File_ReleaseDate { get; set; }
        public string Anime_GroupName { get; set; }
        public string Anime_GroupNameShort { get; set; }
        public int Episode_Rating { get; set; }
        public int Episode_Votes { get; set; }
        public DateTime DateTimeUpdated { get; set; }
        public int IsWatched { get; set; }
        public DateTime? WatchedDate { get; set; }
        public string CRC { get; set; }
        public string MD5 { get; set; }
        public string SHA1 { get; set; }
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public int FileVersion { get; set; }
        public int IsCensored { get; set; }
        public int IsDeprecated { get; set; }
        public int InternalVersion { get; set; }

        #endregion
    }
}
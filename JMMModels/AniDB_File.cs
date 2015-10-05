using System;
using System.Collections.Generic;
using JMMModels.Childs;

namespace JMMModels
{
    public class AniDB_File : DateUpdate
    {
        public string Id { get; set; }
        public int FileId { get; set; }
        public string Hash { get; set; }
        public string Source { get; set; }
        public string VideoCodec { get; set; }
        public string VideoResolution { get; set; }
        public string FileExtension { get; set; }
        public int LengthSeconds { get; set; }
        public string Description { get; set; }
        public DateTime ReleaseDate { get; set; }
        public string GroupId { get; set; }
        public string GroupName { get; set; }
        public string GroupNameShort { get; set; }
        public float Rating { get; set; }
        public int Votes { get; set; }

        public List<UserStats> UserStats { get; set; }


        public string CRC { get; set; }
        public string MD5 { get; set; }
        public string SHA1 { get; set; }
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public int FileVersion { get; set; }
        public bool IsCensored { get; set; }
        public bool IsDeprecated { get; set; }
        public int InternalVersion { get; set; }

        public CrossRefSourceType CrossRefSource { get; set; }
        public List<AniDB_File_Episode> Episodes { get; set; } 
        public List<AudioLanguage> AudioLanguages { get; set; }
        public List<Language> Subtitles { get; set; }  

    }

    public class AniDB_File_Episode
    {
        public string AniDBEpisodeId { get; set; }
        public float Percent { get; set; }
    }
}

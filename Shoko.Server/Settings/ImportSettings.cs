using Shoko.Models.Enums;

namespace Shoko.Server.Settings
{
    public class ImportSettings
    {
        
        public string[] VideoExtensions { get; set; } = new[] { "MKV", "AVI", "MP4", "MOV", "OGM", "WMV", "MPG", "MPEG", "MK3D", "M4V" };

        public RenamingLanguage DefaultSeriesLanguage { get; set; } = RenamingLanguage.Romaji;

        public RenamingLanguage DefaultEpisodeLanguage { get; set; } = RenamingLanguage.Romaji;

        public bool RunOnStart { get; set; } = false;

        public bool ScanDropFoldersOnStart { get; set; } = false;

        public bool Hash_CRC32 { get; set; } = false;
        public bool Hash_MD5 { get; set; } = false;
        public bool Hash_SHA1 { get; set; } = false;

        public bool UseExistingFileWatchedStatus { get; set; } = true;
    }
}
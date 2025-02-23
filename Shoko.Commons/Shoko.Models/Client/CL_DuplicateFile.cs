using System;
using Shoko.Models.Server;

namespace Shoko.Models.Client
{
    public class CL_DuplicateFile
    {
        public int DuplicateFileID { get; set; }
        public string FilePathFile1 { get; set; }
        public string FilePathFile2 { get; set; }
        public string Hash { get; set; }
        public int ImportFolderIDFile1 { get; set; }
        public int ImportFolderIDFile2 { get; set; }
        public DateTime DateTimeUpdated { get; set; }

        public int? AnimeID { get; set; }
        public string AnimeName { get; set; }
        public int? EpisodeType { get; set; }
        public int? EpisodeNumber { get; set; }
        public string EpisodeName { get; set; }

        public int File1VideoLocalPlaceID { get; set; }
        public int File2VideoLocalPlaceID { get; set; }
        public ImportFolder ImportFolder1 { get; set; }
        public ImportFolder ImportFolder2 { get; set; }
    }
}

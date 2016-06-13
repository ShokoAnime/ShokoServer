using System;

namespace JMMContracts
{
    public class Contract_DuplicateFile
    {
        public int DuplicateFileID { get; set; }
        public string FilePathFile1 { get; set; }
        public string FilePathFile2 { get; set; }
        public string Hash { get; set; }
        public int ImportFolderIDFile1 { get; set; }
        public int ImportFolderIDFile2 { get; set; }
        public DateTime DateTimeUpdated { get; set; }

        // data from other entities
        public int? AnimeID { get; set; }
        public string AnimeName { get; set; }
        public int? EpisodeType { get; set; }
        public int? EpisodeNumber { get; set; }
        public string EpisodeName { get; set; }

        public Contract_ImportFolder ImportFolder1 { get; set; }
        public Contract_ImportFolder ImportFolder2 { get; set; }
    }
}
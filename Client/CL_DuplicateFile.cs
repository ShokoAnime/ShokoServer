using Shoko.Models.Server;

namespace Shoko.Models.Client
{
    public class CL_DuplicateFile : DuplicateFile
    {

        // data from other entities
        public int? AnimeID { get; set; }
        public string AnimeName { get; set; }
        public int? EpisodeType { get; set; }
        public int? EpisodeNumber { get; set; }
        public string EpisodeName { get; set; }

        public ImportFolder ImportFolder1 { get; set; }
        public ImportFolder ImportFolder2 { get; set; }
    }
}
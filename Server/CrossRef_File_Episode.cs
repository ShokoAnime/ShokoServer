namespace Shoko.Models.Server
{
    public class CrossRef_File_Episode
    {
        /// <summary>
        /// Stores a reference between the AniDB_File record and an AniDB_Episode
        /// We store the Hash and the FileSize, because this is what enables us to make a call the AniDB UDP API using the FILE command
        /// We store the AnimeID so we can download all the episodes from HTTP API (ie the HTTP api call will return the
        /// anime details and all the episodes
        /// Note 1 - A file can have one to many episodes, and an episode can have one to many files
        /// Note 2 - By storing this information when a user manually associates a file with an episode, we can recover the manual
        /// associations even when they move the files around
        /// Note 3 - We can use a combination of the FileName/FileSize to determine the Hash for a file, this enables us to handle the
        /// moving of files to different locations without needing to re-hash the file again
        /// </summary>
        /// 
        public int CrossRef_File_EpisodeID { get; private set; }
        public string Hash { get; set; }
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public int CrossRefSource { get; set; }
        public int AnimeID { get; set; }
        public int EpisodeID { get; set; }
        public int Percentage { get; set; }
        public int EpisodeOrder { get; set; }

        public CrossRef_File_Episode()
        {
        }
    }
}

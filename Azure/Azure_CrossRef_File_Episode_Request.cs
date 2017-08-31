

namespace Shoko.Models.Azure
{
    public class Azure_CrossRef_File_Episode_Request
    {
        public string Hash { get; set; }
        public int AnimeID { get; set; }
        public int EpisodeID { get; set; }
        public int Percentage { get; set; }
        public int EpisodeOrder { get; set; }
        public string Username { get; set; }
    }
}
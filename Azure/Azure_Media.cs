
namespace Shoko.Models.Azure
{
    public class Azure_Media
    {
        public int MediaID { get; set; }

        public string ED2K { get; set; }

        public byte[] MediaInfo { get; set; }

        public int Version { get; set; }

        public string Username { get; set; }
        public int IsAdminApproved { get; set; }
        public long DateSubmitted { get; set; }


    }
}
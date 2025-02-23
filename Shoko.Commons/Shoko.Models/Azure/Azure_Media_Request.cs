

namespace Shoko.Models.Azure
{
    public class Azure_Media_Request
    {
        public string ED2K { get; set; }
        public byte[] MediaInfo { get; set; }
        public int Version { get; set; }
        public string Username { get; set; }
        public string AuthGUID { get; set; }
    }
}
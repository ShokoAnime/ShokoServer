namespace Shoko.Models.Azure
{
    public class Azure_FileHash_Request
    {
        public string ED2K { get; set; }
        public string CRC32 { get; set; }
        public string MD5 { get; set; }
        public string SHA1 { get; set; }
        public long FileSize { get; set; }

        public string Username { get; set; }
        public string AuthGUID { get; set; }

       
    }
}

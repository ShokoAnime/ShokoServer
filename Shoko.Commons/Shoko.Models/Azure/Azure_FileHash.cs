namespace Shoko.Models.Azure
{
    public class Azure_FileHash
    {
        public int? FileHashID { get; set; }

        public string ED2K { get; set; }
        public string CRC32 { get; set; }
        public string MD5 { get; set; }
        public string SHA1 { get; set; }
        public long FileSize { get; set; }

        public string Username { get; set; }
        public int IsAdminApproved { get; set; }
        public long DateSubmitted { get; set; }

    }
}

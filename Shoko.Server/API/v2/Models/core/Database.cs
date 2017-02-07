namespace Shoko.Server.API.v2.Models.core
{
    public class Database
    {
        public string type { get; set; }
        public string login { get; set; }
        public string password { get; set; }
        public string table { get; set; }
        public string path { get; set; }
        public string server { get; set; }
    }
}

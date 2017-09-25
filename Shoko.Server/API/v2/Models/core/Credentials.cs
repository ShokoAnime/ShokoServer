namespace Shoko.Server.API.v2.Models.core
{
    public class Credentials
    {
        public string login { get; set; }
        public string password { get; set; }
        public int port { get; set; }
        public string token { get; set; }
        public string refresh_token { get; set; }
        public string apikey { get; set; }
        public int apiport { get; set; }
    }
}
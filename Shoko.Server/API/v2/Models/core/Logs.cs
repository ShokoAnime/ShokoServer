namespace Shoko.Server.API.v2.Models.core
{
    public class Logs
    {
        public bool rotate { get; set; }
        public bool zip { get; set; }
        public bool delete { get; set; }
        public int days { get; set; }
    }
}

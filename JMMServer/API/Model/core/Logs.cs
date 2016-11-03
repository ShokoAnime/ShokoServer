namespace JMMServer.API.Model.core
{
    public class Logs
    {
        public bool rotate { get; set; }
        public bool zip { get; set; }
        public bool delete { get; set; }
        public int days { get; set; }
    }
}

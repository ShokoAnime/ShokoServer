namespace JMMServer.API.Model.common
{
    public class Filter
    {
        public int id { get; set; }
        public string name { get; set; }
        public ArtCollection art { get; set; }
        public int size { get; set; }
        public int viewed { get; set;}
        public string url { get; set; }
        public string type { get; set; }

        public Filter()
        {
            art = new ArtCollection();
        }
    }
}

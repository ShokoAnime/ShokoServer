using System.Collections.Generic;

namespace JMMServer.API.Model.common
{
    public class Series
    {
        public List<Serie> series { get; set; }
        public int size { get; set; }
        public string name { get; set; }

        public readonly string type = "series";
    }
}
